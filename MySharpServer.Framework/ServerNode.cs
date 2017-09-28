﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class ServerNode: IServerNode
    {
        public static readonly int PROTOCOL_NONE  = 0;
        public static readonly int PROTOCOL_HTTP  = 1;
        public static readonly int PROTOCOL_WS    = 2;
        public static readonly int PROTOCOL_HTTPS = 3;
        public static readonly int PROTOCOL_WSS   = 4;

        private string m_ServerName = "sharp-server";
        private string m_ServerGroupName = "api";
        private string m_InternalUrl = "http://localhost:9991";
        private string m_PublicUrl = "";
        private int m_PublicProtocol = PROTOCOL_NONE;

        private string m_AccessKey = Guid.NewGuid().ToString();

        private string m_ServerInfoStorage = "SharpNode";

        private IServerLogger m_Logger = null;

        private IDataAccessHelper m_DataHelper = new DataAccessHelper();

        private IJsonCodec m_JsonCodec = new NewtonJsonCodec();

        private ServiceCollection m_LocalServices = null;
        private Dictionary<string, List<string>> m_RemoteServices = null; // contains all (both internal and public) services

        private Dictionary<string, DateTime> m_LocalServiceFiles = new Dictionary<string, DateTime>();

        private bool m_IsUpdatingLocalServices = false;
        private bool m_IsUploadingLocalServerInfo = false;
        private bool m_IsUpdatingRemoteServices = false;

        private Timer m_UpdateLocalServicesTimer = null;   // check dll file and load it if it's newer
        private Timer m_UploadLocalServerInfoTimer = null; // upload all local services, internal access url and public access url
        private Timer m_UpdateRemoteServicesTimer = null;  // auto get all services and internal access urls of all online servers

        private IWebServer m_InternalServer = null;
        private IWebServer m_PublicServer = null;

        public ServerNode(string serverName, string serverGroup, IServerLogger logger = null)
        {
            m_ServerName = serverName;
            m_ServerGroupName = serverGroup;

            m_Logger = logger;
            if (m_Logger == null) m_Logger = new ConsoleLogger();
        }

        public bool Start(ServerSetting internalServerSetting, ServerSetting publicServerSetting = null)
        {
            Stop();

            if (!internalServerSetting.ServerProtocol.ToLower().Contains("http"))
            {
                m_Logger.Error("Internal server supports only HTTP service");
                return false;
            }
            if (internalServerSetting.ServerPort <= 0)
            {
                m_Logger.Error("Invalid internal server port: " + internalServerSetting.ServerPort);
                return false;
            }

            m_InternalServer = new HttpServer(this, m_Logger);

            if (publicServerSetting != null && publicServerSetting.ServerPort > 0)
            {
                if (publicServerSetting.ServerProtocol.ToLower().Contains("http"))
                {
                    m_PublicServer = new HttpServer(this, m_Logger, RequestContext.FLAG_PUBLIC, publicServerSetting.AllowOrigin);
                }
                else if (publicServerSetting.ServerProtocol.ToLower().Contains("ws"))
                {
                    m_PublicServer = new WebSocketServer(this, m_Logger, RequestContext.FLAG_PUBLIC);
                }
            }

            bool isInternalServerOK = true;
            bool isPublicServerOK = true;

            if (m_InternalServer != null && internalServerSetting != null)
            {
                string cert = internalServerSetting.CertFile;
                if (internalServerSetting.ServerProtocol.ToLower().Contains("https"))
                {
                    // if need ssl then just do NOT let "cert" be empty, may set it with "https"
                    if (String.IsNullOrEmpty(cert)) cert = "https";
                }

                isInternalServerOK = m_InternalServer.Start(internalServerSetting.ServerPort, internalServerSetting.ServerIp,
                                                            cert, internalServerSetting.CertKey);
                if (isInternalServerOK)
                {
                    m_InternalUrl = m_InternalServer.GetProtocol() + @"://" + m_InternalServer.GetIp() + ":" + m_InternalServer.GetPort();
                }
                else
                {
                    Stop();
                    return false;
                }
            }
            else
            {
                Stop();
                return false;
            }

            if (isInternalServerOK && isPublicServerOK)
            {
                m_UpdateLocalServicesTimer = new Timer(UpdateLocalServices, m_LocalServiceFiles, 10, 1000 * 10);
                m_UploadLocalServerInfoTimer = new Timer(UploadLocalServerInfo, m_LocalServices, 600, 1000 * 1);
                m_UpdateRemoteServicesTimer = new Timer(UpdateRemoteServices, m_RemoteServices, 800, 1000 * 2);
            }

            for (int i = 0; i < 20; i++) // try to wait till loading local services is done (max waiting time is 1000ms)
            {
                var svclist = m_LocalServices;
                if (svclist == null || svclist.InternalServices == null || svclist.PublicServices == null
                    || (svclist.InternalServices.Count <= 0 && svclist.PublicServices.Count <= 0))
                {
                    Thread.Sleep(50);
                }
                else break;
            }

            if (m_PublicServer != null && publicServerSetting != null)
            {
                string cert = publicServerSetting.CertFile;
                if (publicServerSetting.ServerProtocol.ToLower().Contains("https"))
                {
                    // if need ssl then just do NOT let "cert" be empty, may set it with "https"
                    if (String.IsNullOrEmpty(cert)) cert = "https";
                }

                isPublicServerOK = m_PublicServer.Start(publicServerSetting.ServerPort, publicServerSetting.ServerIp,
                                                        cert, publicServerSetting.CertKey);
                if (isPublicServerOK)
                {
                    var protocol = m_PublicServer.GetProtocol();
                    m_PublicUrl = protocol + @"://" + m_PublicServer.GetIp() + ":" + m_PublicServer.GetPort();

                    if (protocol == "http") m_PublicProtocol = PROTOCOL_HTTP;
                    else if (protocol == "https") m_PublicProtocol = PROTOCOL_HTTPS;
                    else if (protocol == "ws") m_PublicProtocol = PROTOCOL_WS;
                    else if (protocol == "wss") m_PublicProtocol = PROTOCOL_WSS;

                    if (m_PublicProtocol == PROTOCOL_WS || m_PublicProtocol == PROTOCOL_WSS)
                    {
                        ServiceCollection allsvc = m_LocalServices;
                        if (allsvc != null)
                        {
                            IActionCaller svc = null;
                            if (allsvc.InternalServices.TryGetValue("network", out svc))
                            {
                                svc.Call("set-server", m_PublicServer, false);
                            }
                        }
                    }
                }
                else
                {
                    Stop();
                    return false;
                }
            }

            return isInternalServerOK && isPublicServerOK;

        }

        public void Stop()
        {
            if (m_UpdateLocalServicesTimer != null) { m_UpdateLocalServicesTimer.Dispose(); m_UpdateLocalServicesTimer = null; }
            if (m_UploadLocalServerInfoTimer != null) { m_UploadLocalServerInfoTimer.Dispose(); m_UploadLocalServerInfoTimer = null; }
            if (m_UpdateRemoteServicesTimer != null) { m_UpdateRemoteServicesTimer.Dispose(); m_UpdateRemoteServicesTimer = null; }

            if (m_PublicServer != null) { m_PublicServer.Stop(); m_PublicServer = null; }
            if (m_InternalServer != null) { m_InternalServer.Stop(); m_InternalServer = null; }
        }

        public bool IsWorking()
        {
            if (m_InternalServer == null || !m_InternalServer.IsWorking()) return false;

            if (m_PublicServer == null) return true;

            return m_PublicServer.IsWorking();
        }

        public IWebServer GetInternalServer()
        {
            return m_InternalServer;
        }

        public IWebServer GetPublicServer()
        {
            return m_PublicServer;
        }

        public string GetInternalAccessUrl()
        {
            return m_InternalUrl;
        }

        public string GetPublicAccessUrl()
        {
            return m_PublicUrl;
        }

        public string GetName()
        {
            return m_ServerName;
        }

        public string GetGroup()
        {
            return m_ServerGroupName;
        }

        public string GetServerInfoStorage()
        {
            return m_ServerInfoStorage;
        }
        public void SetServerInfoStorage(string value)
        {
            m_ServerInfoStorage = value;
        }

        private void UploadLocalServerInfo(object param)
        {
            if (m_IsUploadingLocalServerInfo) return;
            else m_IsUploadingLocalServerInfo = true;
            try
            {
                if (m_DataHelper == null || !IsWorking()) return; // upload nothing if local server is not working
                using (var cnn = m_DataHelper.OpenDatabase(m_ServerInfoStorage))
                {
                    if (cnn == null) return;
                    var publicServer = m_PublicServer;
                    using (var cmd = cnn.CreateCommand())
                    {
                        int clientCount = 0;
                        if (publicServer != null) clientCount = publicServer.GetClientCount();

                        string svclist = "";
                        ServiceCollection allsvc = m_LocalServices;
                        if (allsvc != null)
                        {
                            foreach (var item in allsvc.PublicServices)
                            {
                                if (svclist.Length == 0) svclist = item.Key;
                                else svclist = svclist + "," + item.Key;
                            }
                            foreach (var item in allsvc.InternalServices)
                            {
                                if (item.Key == "network")
                                {
                                    if (publicServer == null || (m_PublicProtocol != PROTOCOL_WS && m_PublicProtocol != PROTOCOL_WSS))
                                        continue;
                                }
                                if (svclist.Length == 0) svclist = item.Key;
                                else svclist = svclist + "," + item.Key;
                            }
                        }

                        var svrurl = String.Copy(m_InternalUrl);
                        svrurl = svrurl.Replace("//0.0.0.0:", "//127.0.0.1:");

                        m_DataHelper.AddParam(cmd, "@serverName", m_ServerName);
                        m_DataHelper.AddParam(cmd, "@groupName", m_ServerGroupName);
                        m_DataHelper.AddParam(cmd, "@serverUrl", svrurl);
                        m_DataHelper.AddParam(cmd, "@publicUrl", m_PublicUrl);
                        m_DataHelper.AddParam(cmd, "@protocol", m_PublicProtocol);
                        m_DataHelper.AddParam(cmd, "@clients", clientCount);
                        m_DataHelper.AddParam(cmd, "@services", svclist);
                        m_DataHelper.AddParam(cmd, "@accessKey", m_AccessKey);

                        cmd.CommandText = "update db_sharp_node.tbl_server_info " 
                                            + " set group_name = @groupName "
                                            + ", server_url = @serverUrl "
                                            + ", public_url = @publicUrl "
                                            + ", public_protocol = @protocol "
                                            + ", client_count = @clients "
                                            + ", service_list = @services "
                                            + ", access_key = @accessKey "
                                            + ", update_time = CURRENT_TIMESTAMP "
                                            + " where server_name = @serverName "
                                            ;

                        int rows = cmd.ExecuteNonQuery();
                        if (rows == 0)
                        {
                            cmd.CommandText = "insert into db_sharp_node.tbl_server_info "
                                            + " ( server_name, group_name, server_url, public_url, public_protocol, client_count, service_list, access_key, update_time ) values "
                                            + " ( @serverName , @groupName , @serverUrl , @publicUrl , @protocol , @clients , @services , @accessKey , CURRENT_TIMESTAMP ) "
                                            ;

                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                m_Logger.Error("Failed to upload server info: " + ex.Message);
                m_Logger.Error(ex.StackTrace);
            }
            finally
            {
                m_IsUploadingLocalServerInfo = false;
            }
        }

        private void UpdateRemoteServices(object param)
        {
            if (m_IsUpdatingRemoteServices) return;
            else m_IsUpdatingRemoteServices = true;
            try
            {
                if (m_DataHelper == null) return;
                using (var cnn = m_DataHelper.OpenDatabase(m_ServerInfoStorage))
                {
                    if (cnn == null) return;
                    Dictionary<string, List<string>> services = new Dictionary<string, List<string>>();
                    using (var cmd = cnn.CreateCommand())
                    {
                        cmd.CommandText = "select server_name, group_name, server_url, service_list, access_key from db_sharp_node.tbl_server_info "
                                        + " where visibility > 0 and TIMESTAMPDIFF(SECOND, update_time, CURRENT_TIMESTAMP) <= 3 ";

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string serverName = reader["server_name"].ToString();
                                string serverGroupName = reader["group_name"].ToString();
                                string serverUrl = reader["server_url"].ToString();
                                string serviceList = reader["service_list"].ToString();
                                string accessKey = reader["access_key"].ToString();

                                if (serverName.Length <= 0 || serverUrl.Length <= 0 || serviceList.Length <= 0) continue;

                                var svrItem = serverName + "@" + serverGroupName + "|" + serverUrl + (accessKey == null || accessKey.Length <= 0 ? "" : ("|" + accessKey));
                                var svcArray = serviceList.Split(',');
                                foreach (var svc in svcArray)
                                {
                                    List<string> svrList = null;
                                    if (services.TryGetValue(svc, out svrList))
                                    {
                                        if (!svrList.Contains(svrItem)) svrList.Add(svrItem);
                                    }
                                    else
                                    {
                                        svrList = new List<string>();
                                        svrList.Add(svrItem);
                                        services.Add(svc, svrList);
                                    }
                                }
                                
                            }
                        }
                    }
                    m_RemoteServices = services;
                }
            }
            catch (Exception ex)
            {
                m_Logger.Error("Failed to update info of remote services: " + ex.Message);
                m_Logger.Error(ex.StackTrace);
            }
            finally
            {
                m_IsUpdatingRemoteServices = false;
            }
        }

        public void AddLocalServiceFilepath(string filepath)
        {
            var svcfile = String.IsNullOrEmpty(filepath) ? "" : filepath.Trim();
            if (svcfile.Length <= 0) return;
            svcfile = svcfile.Replace('\\', '/');
            if (svcfile[0] != '/' && svcfile.IndexOf(":/") != 1) // if it is not abs path
            {
                string folder = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
                if (folder == null || folder.Trim().Length <= 0)
                {
                    var entry = Assembly.GetEntryAssembly();
                    var location = "";
                    try
                    {
                        if (entry != null) location = entry.Location;
                    }
                    catch { }
                    if (location != null && location.Length > 0)
                    {
                        folder = Path.GetDirectoryName(location);
                    }
                }
                if (folder != null && folder.Length > 0) svcfile = folder.Replace('\\', '/') + "/" + svcfile;
            }
            //m_Logger.Info("Try to add service library: " + svcfile);
            if (!File.Exists(svcfile))
            {
                m_Logger.Error("Service file not found: " + svcfile);
                return;
            }
            if (m_LocalServiceFiles.ContainsKey(svcfile)) return;
            m_LocalServiceFiles.Add(svcfile, DateTime.MinValue);
            m_Logger.Info("Added service library: " + svcfile);
        }

        private void UpdateLocalServices(object param)
        {
            if (m_IsUpdatingLocalServices) return;
            else m_IsUpdatingLocalServices = true;
            try
            {
                Dictionary<string, IActionCaller> publicServices = new Dictionary<string, IActionCaller>();
                Dictionary<string, IActionCaller> internalServices = new Dictionary<string, IActionCaller>();

                Dictionary<string, DateTime> newfiles = new Dictionary<string, DateTime>();

                foreach (var item in m_LocalServiceFiles)
                {
                    if (!File.Exists(item.Key))
                    {
                        m_Logger.Error("Local service library not found: " + item.Key);
                        continue;
                    }
                    DateTime fileDateTime = File.GetLastWriteTime(item.Key);
                    if (fileDateTime > item.Value)
                    {
                        var svclib = Assembly.LoadFrom(item.Key);
                        if (svclib == null) m_Logger.Error("Failed to load service library: " + item.Key);
                        else
                        {
                            //m_Logger.Info("Loaded service library: " + item.Key);

                            var objtypes = svclib.GetTypes();
                            foreach (var objtype in objtypes)
                            {
                                var attr = ServiceWrapper.GetAnnotation(objtype);
                                if (attr != null && attr.Name.Length > 0)
                                {
                                    if (attr.IsPublic)
                                    {
                                        if (attr.Name == "network" || attr.Name == "event") continue;
                                        if (publicServices.ContainsKey(attr.Name)) publicServices.Remove(attr.Name);
                                        publicServices.Add(attr.Name, new ServiceWrapper(objtype, attr.Name, attr.IsPublic, m_Logger));
                                    }
                                    else
                                    {
                                        if (internalServices.ContainsKey(attr.Name)) internalServices.Remove(attr.Name);
                                        internalServices.Add(attr.Name, new ServiceWrapper(objtype, attr.Name, attr.IsPublic, m_Logger));

                                        if (attr.Name == "network")
                                        {
                                            var publicServer = m_PublicServer;
                                            if (publicServer != null && ((m_PublicProtocol == PROTOCOL_WS || m_PublicProtocol == PROTOCOL_WSS)))
                                                internalServices[attr.Name].Call("set-server", publicServer, false);
                                        }
                                    }
                                }
                            }
                            newfiles.Add(item.Key, fileDateTime);
                        }
                    }
                }

                if (newfiles.Count > 0)
                {
                    foreach (var item in newfiles) m_LocalServiceFiles[item.Key] = item.Value;
                }

                if (publicServices.Count > 0 || internalServices.Count > 0)
                {
                    if (m_LocalServices != null && m_LocalServices.PublicServices != null)
                    {
                        foreach (var item in m_LocalServices.PublicServices)
                        {
                            if (!publicServices.ContainsKey(item.Key)) publicServices.Add(item.Key, item.Value);
                        }
                    }

                    if (m_LocalServices != null && m_LocalServices.InternalServices != null)
                    {
                        foreach (var item in m_LocalServices.InternalServices)
                        {
                            if (!internalServices.ContainsKey(item.Key)) internalServices.Add(item.Key, item.Value);
                        }
                    }

                    m_LocalServices = new ServiceCollection(publicServices, internalServices);
                }
                
            }
            catch (Exception ex)
            {
                m_Logger.Error("Failed to update local service: " + ex.Message);
                m_Logger.Error(ex.StackTrace);
            }
            finally
            {
                m_IsUpdatingLocalServices = false;
            }
        }

        public ServiceCollection GetLocalServices()
        {
            return m_LocalServices;
        }

        public object EmitLocalEvent(string eventName, object eventData)
        {
            ServiceCollection allsvc = m_LocalServices;
            if (allsvc != null)
            {
                IActionCaller svc = null;
                if (allsvc.InternalServices.TryGetValue("event", out svc))
                {
                    return svc.Call(eventName, eventData, false);
                }
            }
            return null;
        }

        public void HandleRequest(RequestContext request)
        {
            if (request.PathParts.Count < 2) request.Session.CloseConnection();
            else
            {
                request.DataHelper = m_DataHelper;
                request.JsonCodec = m_JsonCodec;
                request.LocalLogger = m_Logger;
                request.LocalServer = m_ServerName + (String.IsNullOrEmpty(m_ServerGroupName) ? "" : ("@" + m_ServerGroupName));
                request.LocalServices = m_LocalServices;
                request.RemoteServices = m_RemoteServices;
                if (request.IsFromPublic())
                {
                    var group = request.Session.GetGroup();
                    request.EntryServer = request.LocalServer;
                    request.ClientAddress = request.Session.GetRemoteAddress() + (String.IsNullOrEmpty(group) ? "" : ("@" + group));
                    HandlePublicRequest(request);
                }
                else HandleInternalRequest(request);
            }
        }

        private void HandlePublicRequest(RequestContext request)
        {
            //m_Logger.Info("HandlePublicRequest() - " + request.PathParts[0] + "::" + request.PathParts[1]);

            string serviceName = request.PathParts[0];
            string actionName = request.PathParts[1];

            IActionCaller caller = null;
            List<string> remoteServers = null;
            if (request.LocalServices != null && request.LocalServices.PublicServices.TryGetValue(serviceName, out caller))
            {
                ServiceWrapper svc = caller as ServiceWrapper;
                string errormsg = svc != null ? svc.ValidateRequest(request) : "Invalid service";
                if (errormsg == null || errormsg.Length <= 0)
                {
                    var tasks = svc.GetTaskFactory(request);
                    if (tasks != null) tasks.StartNew((param) => ProcessData(param), new ServiceRequestContext(svc, request, true));
                    else ProcessData(new ServiceRequestContext(svc, request, true)); // process it in main thread (single thread)
                }
                //else request.Session.CloseConnection();
                else
                {
                    request.Session.BeginResponse();
                    request.Session.Send("{'error': '" + errormsg + "'}");
                    request.Session.EndResponse();
                }
            }
            else if (request.RemoteServices != null && request.RemoteServices.TryGetValue(serviceName, out remoteServers))
            {
                string result = "";
                if (remoteServers != null && remoteServers.Count > 0)
                {
                    var remoteInfoParts = RandomPicker.Pick<string>(remoteServers).Split('|');
                    if (remoteInfoParts.Length >= 2)
                    {
                        string remoteUrl = remoteInfoParts[1]; // name | url | key
                        try
                        {
                            result = RemoteCaller.Call(remoteUrl, serviceName, actionName, 
                                                        request.EntryServer + "/" + request.ClientAddress + "/" + request.Data);
                        }
                        catch (Exception ex)
                        {
                            result = "";
                            m_Logger.Error("Failed to call remote service! URL: " + remoteUrl + " , ServiceName: " + serviceName
                                            + " , ActionName: " + request.PathParts[1] + " , Data: " + request.PathParts.Last() + " , Error: " + ex.Message);
                        }
                    }
                }
                request.Session.BeginResponse();
                if (result != null && result.Length > 0) request.Session.Send(result);
                request.Session.EndResponse();
            }
            else request.Session.EndResponse();
        }

        private void HandleInternalRequest(RequestContext request)
        {
            //m_Logger.Info("HandleInternalRequest() - " + request.PathParts[0] + "::" + request.PathParts[1]);

            string serviceName = request.PathParts[0];
            string accessKey = request.Key;
            if (accessKey != null && accessKey.Length > 0 && accessKey == m_AccessKey)
            {
                IActionCaller caller = null;
                if (request.LocalServices != null)
                {
                    if (!request.LocalServices.PublicServices.TryGetValue(serviceName, out caller))
                    {
                        caller = null;
                        if (!request.LocalServices.InternalServices.TryGetValue(serviceName, out caller))
                        {
                            caller = null;
                        }
                    }
                }
                if (caller == null) request.Session.EndResponse();
                else
                {
                    ServiceWrapper svc = caller as ServiceWrapper;
                    string errormsg = svc != null ? svc.ValidateRequest(request) : "Invalid service";
                    if (errormsg == null || errormsg.Length <= 0)
                    {
                        var tasks = svc.GetTaskFactory(request);
                        if (tasks != null) tasks.StartNew((param) => ProcessData(param), new ServiceRequestContext(svc, request, false));
                        else ProcessData(new ServiceRequestContext(svc, request, false)); // process it in main thread (single thread)
                    }
                    //else request.Session.EndResponse();
                    else
                    {
                        request.Session.BeginResponse();
                        request.Session.Send("{'error': '" + errormsg + "'}");
                        request.Session.EndResponse();
                    }
                }
            }
            else
            {
                IActionCaller caller = null;
                if (request.LocalServices != null)
                {
                    if (!request.LocalServices.PublicServices.TryGetValue(serviceName, out caller))
                    {
                        caller = null;
                    }
                }
                if (caller == null) request.Session.EndResponse();
                else
                {
                    ServiceWrapper svc = caller as ServiceWrapper;
                    string errormsg = svc != null ? svc.ValidateRequest(request) : "Invalid service";
                    if (errormsg == null || errormsg.Length <= 0)
                    {
                        var tasks = svc.GetTaskFactory(request);
                        if (tasks != null) tasks.StartNew((param) => ProcessData(param), new ServiceRequestContext(svc, request, true));
                        else ProcessData(new ServiceRequestContext(svc, request, true)); // process it in main thread (single thread)
                    }
                    //else request.Session.EndResponse();
                    else
                    {
                        request.Session.BeginResponse();
                        request.Session.Send("{'error': '" + errormsg + "'}");
                        request.Session.EndResponse();
                    }
                }
            }
            
        }

        private void ProcessData(object obj)
        {
            ServiceRequestContext ctx = obj as ServiceRequestContext;
            if (ctx == null) return;
            try
            {
                ctx.Context.Session.BeginResponse();
                ctx.Service.Call(ctx.Context.PathParts[1], ctx.Context, ctx.IsPublicRequest);
                ctx.Context.Session.EndResponse();
            }
            catch (Exception ex)
            {
                m_Logger.Error("Process request error: " + ex.Message);
                m_Logger.Error(ex.StackTrace);
            }
        }

    }
}