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

        private bool m_InternalDefaultMultithreading = false;
        private bool m_PublicDefaultMultithreading = false;
        private string m_InternalCustomRouterName = "";
        private string m_PublicCustomRouterName = "";

        //private IActionCaller m_InternalCustomRouter = null;
        //private IActionCaller m_PublicCustomRouter = null;

        private string m_AccessKey = Guid.NewGuid().ToString();

        private string m_ServerInfoStorage = "SharpNode";

        private IServerLogger m_Logger = null;

        private IDataAccessHelper m_DataHelper = new DataAccessHelper();

        private IJsonCodec m_JsonCodec = new NewtonJsonCodec();

        private ServiceCollection m_LocalServices = null;
        private Dictionary<string, List<string>> m_RemoteServices = null; // contains all (both internal and public) services

        private Dictionary<string, DateTime> m_LocalServiceFiles = new Dictionary<string, DateTime>();

        private ConcurrentDictionary<string, ServiceWrapper> m_AllCreatedServices = new ConcurrentDictionary<string, ServiceWrapper>();

        private bool m_IsUpdatingLocalServices = false;
        private bool m_IsUploadingLocalServerInfo = false;
        private bool m_IsUpdatingRemoteServices = false;

        private int m_RemoteInfoExpiryTime = 5; // in seconds

        private bool m_IsStarting = false;
        private bool m_IsStopping = false;

        private Timer m_UpdateLocalServicesTimer = null;   // check dll file and load it if it's newer
        private Timer m_UploadLocalServerInfoTimer = null; // upload all local services, internal access url and public access url
        private Timer m_UpdateRemoteServicesTimer = null;  // auto get all services and internal access urls of all online servers

        private IWebServer m_InternalServer = null;
        private IWebServer m_PublicServer = null;

        public Func<IWebServer, IWebServer> OnCreatePublicServer = null;
        //public Action<IWebSession> OnWebSocketConnect = null;
        //public Action<IWebSession> OnWebSocketDisconnect = null;

        public ServerNode(string serverName, string serverGroup, IServerLogger logger = null)
        {
            m_ServerName = serverName;
            m_ServerGroupName = serverGroup;

            m_Logger = logger;
            if (m_Logger == null) m_Logger = new ConsoleLogger();

            if (RemoteCaller.JsonCodec == null || RemoteCaller.JsonCodec is SimpleJsonCodec)
                RemoteCaller.JsonCodec = new NewtonJsonCodec();
        }

        public IServerLogger GetLogger()
        {
            return m_Logger;
        }

        public IJsonCodec GetJsonHelper()
        {
            return m_JsonCodec;
        }

        public IDataAccessHelper GetDataHelper()
        {
            return m_DataHelper;
        }

        public Dictionary<string, List<string>> GetRemoteServices()
        {
            var services = m_RemoteServices;
            return services == null ? new Dictionary<string, List<string>>() : new Dictionary<string, List<string>>(services);
        }

        public string GetDefaultMultithreading()
        {
            string result = "";
            if (m_InternalDefaultMultithreading) result = "internal";
            if (m_PublicDefaultMultithreading)
            {
                if (result.Length <= 0) result = "public";
                else result += ",public";
            }
            return result.Length > 0 ? result : "none";
        }
        public string GetCustomRouterNames()
        {
            string result1 = "[ - ]";
            string result2 = "[ - ]";
            if (!string.IsNullOrEmpty(m_InternalCustomRouterName)) result1 ="[" + m_InternalCustomRouterName + "]";
            if (!string.IsNullOrEmpty(m_PublicCustomRouterName)) result2 = "[" + m_PublicCustomRouterName + "]";
            return result1 + " | " + result2;
        }

        public bool IsStarting()
        {
            return m_IsStarting;
        }

        public bool IsStopping()
        {
            return m_IsStopping;
        }

        public async Task<bool> Start(ServerSetting internalServerSetting, ServerSetting publicServerSetting = null)
        {
            if (internalServerSetting == null) return false;

            await Stop();

            if (m_IsStarting) return false;
            m_IsStarting = true;

            try
            {
                m_InternalDefaultMultithreading = internalServerSetting.IsDefaultMultithreading;
                m_InternalCustomRouterName = internalServerSetting.CustomRouterService;

                if (internalServerSetting.Expiration > 0) m_RemoteInfoExpiryTime = internalServerSetting.Expiration;

                string internalProtocol = internalServerSetting.WorkProtocol.ToLower();

                if (!internalProtocol.Contains("http"))
                {
                    m_Logger.Error("Internal server supports only HTTP listener");
                    return false;
                }
                if (internalServerSetting.WorkPort <= 0)
                {
                    m_Logger.Error("Invalid internal server port: " + internalServerSetting.WorkPort);
                    return false;
                }

                if (internalServerSetting.WorkProtocol.ToLower().Contains("simple-http"))
                {
                    m_InternalServer = new SimpleHttpServer(this, m_Logger);
                }
                if (m_InternalServer == null) m_InternalServer = new HttpServer(this, m_Logger);

                if (publicServerSetting != null && publicServerSetting.WorkPort > 0)
                {
                    m_PublicDefaultMultithreading = publicServerSetting.IsDefaultMultithreading;
                    m_PublicCustomRouterName = publicServerSetting.CustomRouterService;

                    if (publicServerSetting.WorkProtocol.ToLower().Contains("http"))
                    {
                        var flags = RequestContext.FLAG_PUBLIC;
                        if (publicServerSetting.AllowParentPath) flags = flags | RequestContext.FLAG_ALLOW_PARENT_PATH;
                        if (publicServerSetting.WorkProtocol.ToLower().Contains("simple-http"))
                        {
                            m_PublicServer = new SimpleHttpServer(this, m_Logger, flags, publicServerSetting.AllowOrigin);
                        }
                        if (m_PublicServer == null) m_PublicServer = new HttpServer(this, m_Logger, flags, publicServerSetting.AllowOrigin);
                    }
                    else if (publicServerSetting.WorkProtocol.ToLower().Contains("ws"))
                    {
                        m_PublicServer = new WebSocketServer(this, m_Logger, RequestContext.FLAG_PUBLIC);
                    }
                    if (OnCreatePublicServer != null) m_PublicServer = OnCreatePublicServer(m_PublicServer);
                }

                bool isInternalServerOK = true;
                bool isPublicServerOK = true;

                if (m_InternalServer != null && internalServerSetting != null)
                {
                    string cert = internalServerSetting.CertFile;
                    if (internalServerSetting.WorkProtocol.ToLower().Contains("https"))
                    {
                        // if need ssl then just do NOT let "cert" be empty, may set it with "https"
                        if (String.IsNullOrEmpty(cert)) cert = "https";
                    }

                    isInternalServerOK = m_InternalServer.Start(internalServerSetting.WorkPort, internalServerSetting.WorkIp,
                                                                cert, internalServerSetting.CertKey);
                    if (isInternalServerOK)
                    {
                        if (internalServerSetting.AccessUrl.Length > 0) m_InternalUrl = internalServerSetting.AccessUrl;
                        else m_InternalUrl = m_InternalServer.GetProtocol() + @"://" + m_InternalServer.GetIp() + ":" + m_InternalServer.GetPort();
                    }
                    else
                    {
                        await Stop();
                        return false;
                    }
                }
                else
                {
                    await Stop();
                    return false;
                }

                if (isInternalServerOK && isPublicServerOK && m_LocalServiceFiles.Count > 0)
                {
                    await UpdateLocalServicesAsync(); // let's try to load local services from files first...
                }

                await Task.Delay(100);

                if (isInternalServerOK && isPublicServerOK)
                {
                    m_UpdateLocalServicesTimer = new Timer(UpdateLocalServices, m_LocalServiceFiles, 10, 1000 * 10);
                    m_UploadLocalServerInfoTimer = new Timer(UploadLocalServerInfo, m_LocalServices, 600, 1000 * 1);
                    m_UpdateRemoteServicesTimer = new Timer(UpdateRemoteServices, m_RemoteServices, 800, 1000 * 2);
                }

                await Task.Delay(100);

                if (m_InternalServer != null && internalServerSetting != null)
                {
                    if (isInternalServerOK)
                    {
                        // ...
                    }
                }

                if (m_PublicServer != null && publicServerSetting != null)
                {
                    string cert = publicServerSetting.CertFile;
                    if (publicServerSetting.WorkProtocol.ToLower().Contains("https"))
                    {
                        // if need ssl then just do NOT let "cert" be empty, may set it with "https"
                        if (String.IsNullOrEmpty(cert)) cert = "https";
                    }

                    isPublicServerOK = m_PublicServer.Start(publicServerSetting.WorkPort, publicServerSetting.WorkIp,
                                                            cert, publicServerSetting.CertKey);
                    if (isPublicServerOK)
                    {
                        var protocol = m_PublicServer.GetProtocol();

                        if (publicServerSetting.AccessUrl.Length > 0) m_PublicUrl = publicServerSetting.AccessUrl;
                        else m_PublicUrl = protocol + @"://" + m_PublicServer.GetIp() + ":" + m_PublicServer.GetPort();

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
                                    await svc.Call("set-server", m_PublicServer, false);
                                }
                            }
                        }
                    }
                    else
                    {
                        await Stop();
                        return false;
                    }
                }

                return isInternalServerOK && isPublicServerOK;

            }
            finally
            {
                m_IsStarting = false;
            }

        }

        public bool IsStandalone()
        {
            return (m_PublicServer != null && m_InternalServer == null) 
                && String.IsNullOrEmpty(m_ServerInfoStorage);
        }

        public async Task<bool> StartStandaloneMode(ServerSetting publicServerSetting)
        {
            if (publicServerSetting == null) return false;

            await Stop();

            if (m_IsStarting) return false;
            m_IsStarting = true;

            try
            {
                SetServerInfoStorage("");

                if (publicServerSetting != null && publicServerSetting.WorkPort > 0)
                {
                    m_PublicDefaultMultithreading = publicServerSetting.IsDefaultMultithreading;
                    m_PublicCustomRouterName = publicServerSetting.CustomRouterService;

                    if (publicServerSetting.WorkProtocol.ToLower().Contains("http"))
                    {
                        var flags = RequestContext.FLAG_PUBLIC;
                        if (publicServerSetting.AllowParentPath) flags = flags | RequestContext.FLAG_ALLOW_PARENT_PATH;
                        if (publicServerSetting.WorkProtocol.ToLower().Contains("simple-http"))
                        {
                            m_PublicServer = new SimpleHttpServer(this, m_Logger, flags, publicServerSetting.AllowOrigin);
                        }
                        if (m_PublicServer == null) m_PublicServer = new HttpServer(this, m_Logger, flags, publicServerSetting.AllowOrigin);
                    }
                    else if (publicServerSetting.WorkProtocol.ToLower().Contains("ws"))
                    {
                        m_PublicServer = new WebSocketServer(this, m_Logger, RequestContext.FLAG_PUBLIC);
                    }
                    if (OnCreatePublicServer != null) m_PublicServer = OnCreatePublicServer(m_PublicServer);
                }

                bool isPublicServerOK = true;

                if (isPublicServerOK && m_LocalServiceFiles.Count > 0)
                {
                    await UpdateLocalServicesAsync(); // let's try to load local services from files first...
                }

                await Task.Delay(100);

                if (isPublicServerOK)
                {
                    m_UpdateLocalServicesTimer = new Timer(UpdateLocalServices, m_LocalServiceFiles, 10, 1000 * 10);
                    m_UploadLocalServerInfoTimer = new Timer(UploadLocalServerInfo, m_LocalServices, 600, 1000 * 1);
                    m_UpdateRemoteServicesTimer = new Timer(UpdateRemoteServices, m_RemoteServices, 800, 1000 * 2);
                }

                await Task.Delay(100);

                if (m_PublicServer != null && publicServerSetting != null)
                {
                    string cert = publicServerSetting.CertFile;
                    if (publicServerSetting.WorkProtocol.ToLower().Contains("https"))
                    {
                        // if need ssl then just do NOT let "cert" be empty, may set it with "https"
                        if (String.IsNullOrEmpty(cert)) cert = "https";
                    }

                    isPublicServerOK = m_PublicServer.Start(publicServerSetting.WorkPort, publicServerSetting.WorkIp,
                                                            cert, publicServerSetting.CertKey);
                    if (isPublicServerOK)
                    {
                        var protocol = m_PublicServer.GetProtocol();

                        if (publicServerSetting.AccessUrl.Length > 0) m_PublicUrl = publicServerSetting.AccessUrl;
                        else m_PublicUrl = protocol + @"://" + m_PublicServer.GetIp() + ":" + m_PublicServer.GetPort();

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
                                    await svc.Call("set-server", m_PublicServer, false);
                                }
                            }
                        }
                    }
                    else
                    {
                        await Stop();
                        return false;
                    }
                }
                else
                {
                    await Stop();
                    return false;
                }

                return isPublicServerOK;

            }
            finally
            {
                m_IsStarting = false;
            }

        }

        public async Task Stop()
        {
            //m_Logger.Info("Call Stop()");

            if (m_IsStopping) return;
            m_IsStopping = true;

            try
            {
                if (m_UpdateLocalServicesTimer != null) { m_UpdateLocalServicesTimer.Dispose(); m_UpdateLocalServicesTimer = null; }
                if (m_UploadLocalServerInfoTimer != null) { m_UploadLocalServerInfoTimer.Dispose(); m_UploadLocalServerInfoTimer = null; }
                if (m_UpdateRemoteServicesTimer != null) { m_UpdateRemoteServicesTimer.Dispose(); m_UpdateRemoteServicesTimer = null; }

                if (m_PublicServer != null) { m_PublicServer.Stop(); m_PublicServer = null; }
                if (m_InternalServer != null) { m_InternalServer.Stop(); m_InternalServer = null; }

                foreach (var item in m_AllCreatedServices)
                {
                    var oldone = m_AllCreatedServices[item.Key] as ServiceWrapper;
                    if (oldone != null)
                    {
                        string errmsg = await oldone.Unload(this);
                        if (!String.IsNullOrEmpty(errmsg))
                        {
                            m_Logger.Error("Failed to unload service [" + item.Key + "] - error: " + errmsg);
                            //Console.WriteLine("Failed to unload service [" + item.Key + "] - error: " + errmsg);
                        }
                        else
                        {
                            Console.WriteLine("Unloaded service [" + item.Key + "]");
                        }
                    }
                }
                m_AllCreatedServices.Clear();

                ResetLocalServiceFiles();

            }
            finally
            {
                m_IsStopping = false;
            }

        }

        public bool IsWorking()
        {
            if (m_InternalServer != null && !m_InternalServer.IsWorking()) return false;

            if (m_PublicServer != null && !m_PublicServer.IsWorking()) return false;

            return m_InternalServer != null || m_PublicServer != null;
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
            m_ServerInfoStorage = value; // may set it to empty string if want to run in standalone mode
        }

        private void UploadLocalServerInfo(object param)
        {
            if (m_PublicServer == null && m_InternalServer == null) return;

            if (m_ServerInfoStorage == null || m_ServerInfoStorage.Length <= 0) return;
            if (m_IsUploadingLocalServerInfo) return;
            else m_IsUploadingLocalServerInfo = true;
            try
            {
                if (m_DataHelper == null || !IsWorking()) return; // upload nothing if local server is not working
                using (var cnn = m_DataHelper.OpenDatabase(m_ServerInfoStorage))
                {
                    //if (cnn == null) return;
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

                        cmd.CommandText = "update tbl_server_info " 
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
                            cmd.CommandText = "insert into tbl_server_info "
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
            if (m_PublicServer == null && m_InternalServer == null) return;

            if (m_ServerInfoStorage == null || m_ServerInfoStorage.Length <= 0) return;
            if (m_IsUpdatingRemoteServices) return;
            else m_IsUpdatingRemoteServices = true;
            try
            {
                if (m_DataHelper == null) return;
                using (var cnn = m_DataHelper.OpenDatabase(m_ServerInfoStorage))
                {
                    //if (cnn == null) return;
                    Dictionary<string, List<string>> services = new Dictionary<string, List<string>>();
                    using (var cmd = cnn.CreateCommand())
                    {
                        cmd.CommandText = "select server_name, group_name, public_url, server_url, service_list, access_key from tbl_server_info "
                                        + " where visibility > 0 and TIMESTAMPDIFF(SECOND, update_time, CURRENT_TIMESTAMP) <= " + m_RemoteInfoExpiryTime;

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

                                var publicUrlValue = reader["public_url"];
                                string publicUrl = publicUrlValue is System.DBNull ? "" : publicUrlValue as string;
                                if (publicUrl == null) publicUrl = "";

                                var svrItem = serverName + "@" + serverGroupName + "|" 
                                            + (serverUrl + (String.IsNullOrEmpty(publicUrl) ? "" : ("," + publicUrl))) 
                                            + (String.IsNullOrEmpty(accessKey) ? "" : ("|" + accessKey));

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

        public void ResetLocalServiceFiles(List<string> filepaths = null)
        {
            //m_Logger.Info("Call ResetLocalServiceFiles(): " + (filepaths == null ? "null" : filepaths.Count.ToString()));

            Dictionary<string, DateTime> localServiceFiles = new Dictionary<string, DateTime>();

            List<string> svcFilepaths = new List<string>();
            if (filepaths == null)
            {
                var svcFiles = m_LocalServiceFiles;
                svcFilepaths.AddRange(svcFiles.Keys);
            }
            else
            {
                svcFilepaths.AddRange(filepaths);
            }

            List<string> fullFilepaths = new List<string>();
            foreach (var svcfile in svcFilepaths)
            {
                var pathItem = AddLocalServiceFilepath(svcfile, localServiceFiles);
                if (!String.IsNullOrEmpty(pathItem)) fullFilepaths.Add(pathItem);
            }

            if (filepaths != null)
            {
                foreach (var itemFile in fullFilepaths)
                    m_Logger.Info("Found plug-in: " + itemFile);
            }

            m_LocalServiceFiles = localServiceFiles;
        }

        private string AddLocalServiceFilepath(string filepath, Dictionary<string, DateTime> localServiceFiles)
        {
            var svcfile = String.IsNullOrEmpty(filepath) ? "" : filepath.Trim();
            if (svcfile.Length <= 0) return "";
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
                return "";
            }
            else
            {
                svcfile = new FileInfo(svcfile).FullName;
            }

            if (localServiceFiles.ContainsKey(svcfile)) return "";
            localServiceFiles.Add(svcfile, DateTime.MinValue);

            return svcfile;

            //m_Logger.Info("Reset service library file: " + svcfile);
        }

        private async Task UpdateLocalServicesAsync()
        {
            if (m_PublicServer == null && m_InternalServer == null) return;

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

                    //m_Logger.Info("Try to update local lib file: " + item.Key);
                    //m_Logger.Info("Original file date: " + item.Value.ToString("yyyy-MM-dd HH:mm:ss"));
                    //m_Logger.Info("Current file date : " + fileDateTime.ToString("yyyy-MM-dd HH:mm:ss"));

                    if (fileDateTime > item.Value)
                    {
                        //var svclib = Assembly.LoadFrom(item.Key);
                        byte[] assemblyBytes = File.ReadAllBytes(item.Key);
                        var svclib = Assembly.Load(assemblyBytes);
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
                                    if (m_AllCreatedServices.ContainsKey(attr.Name))
                                    {
                                        var oldone = m_AllCreatedServices[attr.Name] as ServiceWrapper;
                                        if (oldone != null)
                                        {
                                            string errmsg = await oldone.Unload(this);
                                            if (!String.IsNullOrEmpty(errmsg))
                                            {
                                                m_Logger.Error("Failed to unload service " + attr.Name + " - error: " + errmsg);
                                            }
                                        }
                                    }

                                    if (attr.IsPublic)
                                    {
                                        if (attr.Name == "network" || attr.Name == "event")
                                        {
                                            //m_Logger.Warn("Network or Event service should not be public!");
                                            throw new Exception("Network or Event service should not be public!");
                                        }
                                        if (publicServices.ContainsKey(attr.Name)) publicServices.Remove(attr.Name);
                                        var newone = new ServiceWrapper(objtype, attr.Name, attr.IsPublic, m_Logger);
                                        string errormsg = await newone.Load(this);
                                        if (!String.IsNullOrEmpty(errormsg))
                                        {
                                            m_Logger.Error("Failed to load service " + attr.Name + " - error: " + errormsg);
                                            continue;
                                        }
                                        m_AllCreatedServices[attr.Name] = newone;
                                        publicServices.Add(attr.Name, newone);
                                        m_Logger.Info("Loaded public service [" + attr.Name + "]");

                                    }
                                    else
                                    {
                                        if (internalServices.ContainsKey(attr.Name)) internalServices.Remove(attr.Name);
                                        var newone = new ServiceWrapper(objtype, attr.Name, attr.IsPublic, m_Logger);
                                        string errormsg = await newone.Load(this);
                                        if (!String.IsNullOrEmpty(errormsg))
                                        {
                                            m_Logger.Error("Failed to init service " + attr.Name + " - " + errormsg);
                                            continue;
                                        }
                                        m_AllCreatedServices[attr.Name] = newone;
                                        internalServices.Add(attr.Name, newone);
                                        m_Logger.Info("Loaded internal service [" + attr.Name + "]");

                                        if (attr.Name == "network")
                                        {
                                            var publicServer = m_PublicServer;
                                            if (publicServer != null && ((m_PublicProtocol == PROTOCOL_WS || m_PublicProtocol == PROTOCOL_WSS)))
                                                await internalServices[attr.Name].Call("set-server", publicServer, false);
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
                m_Logger.Error("Failed to update local services: " + ex.Message);
                m_Logger.Error(ex.StackTrace);
            }
            finally
            {
                m_IsUpdatingLocalServices = false;
            }
        }

        private async void UpdateLocalServices(object param)
        {
            await UpdateLocalServicesAsync();
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
                //IActionCaller svc = null;
                //if (allsvc.InternalServices.TryGetValue("event", out svc))
                //{
                //    return svc.Call(eventName, eventData, false);
                //}

                // Local events need local functions only...
                foreach (var item in allsvc.InternalServices)
                {
                    item.Value.LocalCall(eventName, eventData);
                }
                foreach (var item in allsvc.PublicServices)
                {
                    item.Value.LocalCall(eventName, eventData);
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
                request.JsonHelper = m_JsonCodec;
                request.Logger = m_Logger;
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

        private async void HandlePublicRequest(RequestContext request)
        {
            //m_Logger.Info("HandlePublicRequest() - " + request.RequestService + "::" + request.RequestAction);

            string serviceName = request.RequestService;
            string actionName = request.RequestAction;

            if (!String.IsNullOrEmpty(m_PublicCustomRouterName))
            {
                ServiceCollection allsvc = m_LocalServices;
                if (allsvc != null)
                {
                    IActionCaller svc = null;
                    allsvc.PublicServices.TryGetValue(m_PublicCustomRouterName, out svc);
                    if (svc == null) allsvc.InternalServices.TryGetValue(m_PublicCustomRouterName, out svc);
                    if (svc != null)
                    {
                        var rsl = await svc.Call("get-service", request, false);
                        if (rsl != null)
                        {
                            var newServiceAction = rsl.ToString().Split('|'); // service name | action name
                            var newServiceName = newServiceAction.Length > 0 ? newServiceAction[0].Trim() : "";
                            var newActionName = newServiceAction.Length > 1 ? newServiceAction[1].Trim() : "";
                            if (!string.IsNullOrEmpty(newServiceName))
                            {
                                request.TargetService = newServiceName;
                                serviceName = newServiceName;
                            }
                            if (!string.IsNullOrEmpty(newActionName))
                            {
                                request.TargetAction = newActionName;
                                actionName = newActionName;
                            }
                        }
                    }
                }
            }

            IActionCaller caller = null;
            List<string> remoteServers = null;
            if (request.LocalServices != null && request.LocalServices.PublicServices.TryGetValue(serviceName, out caller))
            {
                ServiceWrapper svc = caller as ServiceWrapper;
                string errormsg = svc != null ? await svc.ValidateRequest(request) : "Invalid service";
                if (errormsg == null || errormsg.Length <= 0)
                {
                    var tasks = await svc.GetTaskFactory(request);
                    if (tasks == null && m_PublicDefaultMultithreading) tasks = Task.Factory;
                    if (tasks != null) await tasks.StartNew((param) => ProcessData(param), new ServiceRequestContext(svc, request, true)).ConfigureAwait(false);
                    else ProcessData(new ServiceRequestContext(svc, request, true)); // process it in listener's thread
                }
                //else request.Session.CloseConnection();
                else
                {
                    request.Session.BeginResponse();
                    await request.Session.Send(GetJsonHelper().ToJsonString(new { error = errormsg }));
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
                        string remoteUrl = remoteInfoParts[1].Split(',')[0]; // name | url | key
                        try
                        {
                            result = await RemoteCaller.Call(remoteUrl, serviceName, actionName, 
                                                        request.EntryServer + "/" + request.ClientAddress + "/" + request.Data);
                        }
                        catch (Exception ex)
                        {
                            result = "";
                            m_Logger.Error("Failed to call remote service! URL: " + remoteUrl + " , ServiceName: " + serviceName
                                            + " , ActionName: " + request.RequestAction + " , Data: " + request.PathParts.Last() + " , Error: " + ex.Message);
                        }
                    }
                }
                request.Session.BeginResponse();
                await request.Session.Send(result == null ? "" : result);
                request.Session.EndResponse();
            }
            else request.Session.EndResponse();
        }

        private async void HandleInternalRequest(RequestContext request)
        {
            //m_Logger.Info("HandleInternalRequest() - " + request.RequestService + "::" + request.RequestAction);

            string serviceName = request.RequestService;

            if (!String.IsNullOrEmpty(m_InternalCustomRouterName))
            {
                ServiceCollection allsvc = m_LocalServices;
                if (allsvc != null)
                {
                    IActionCaller svc = null;
                    allsvc.PublicServices.TryGetValue(m_InternalCustomRouterName, out svc);
                    if (svc == null) allsvc.InternalServices.TryGetValue(m_InternalCustomRouterName, out svc);
                    if (svc != null)
                    {
                        var rsl = await svc.Call("get-service", request, false);
                        if (rsl != null)
                        {
                            var newServiceAction = rsl.ToString().Split('|'); // service name | action name
                            var newServiceName = newServiceAction.Length > 0 ? newServiceAction[0].Trim() : "";
                            var newActionName = newServiceAction.Length > 1 ? newServiceAction[1].Trim() : "";
                            if (!string.IsNullOrEmpty(newServiceName))
                            {
                                request.TargetService = newServiceName;
                                serviceName = newServiceName;
                            }
                            if (!string.IsNullOrEmpty(newActionName))
                            {
                                request.TargetAction = newActionName;
                                //actionName = newActionName;
                            }
                        }
                    }
                }
            }

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
                    string errormsg = svc != null ? await svc.ValidateRequest(request) : "Invalid service";
                    if (errormsg == null || errormsg.Length <= 0)
                    {
                        var tasks = await svc.GetTaskFactory(request);
                        if (tasks == null && m_InternalDefaultMultithreading) tasks = Task.Factory;
                        if (tasks != null) await tasks.StartNew((param) => ProcessData(param), new ServiceRequestContext(svc, request, false)).ConfigureAwait(false);
                        else ProcessData(new ServiceRequestContext(svc, request, false)); // process it in listener's thread
                    }
                    //else request.Session.EndResponse();
                    else
                    {
                        request.Session.BeginResponse();
                        await request.Session.Send(GetJsonHelper().ToJsonString(new { error = errormsg }));
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
                    string errormsg = svc != null ? await svc.ValidateRequest(request) : "Invalid service";
                    if (errormsg == null || errormsg.Length <= 0)
                    {
                        var tasks = await svc.GetTaskFactory(request);
                        if (tasks == null && m_InternalDefaultMultithreading) tasks = Task.Factory;
                        if (tasks != null) await tasks.StartNew((param) => ProcessData(param), new ServiceRequestContext(svc, request, true)).ConfigureAwait(false);
                        else ProcessData(new ServiceRequestContext(svc, request, true)); // process it in listener's thread
                    }
                    //else request.Session.EndResponse();
                    else
                    {
                        request.Session.BeginResponse();
                        await request.Session.Send(GetJsonHelper().ToJsonString(new { error = errormsg }));
                        request.Session.EndResponse();
                    }
                }
            }
            
        }

        private async void ProcessData(object obj)
        {
            ServiceRequestContext ctx = obj as ServiceRequestContext;
            if (ctx == null) return;
            try
            {
                ctx.Context.Session.BeginResponse();
                await ctx.Service.Call(string.IsNullOrEmpty(ctx.Context.TargetAction) ? ctx.Context.RequestAction : ctx.Context.TargetAction, 
                                        ctx.Context, ctx.IsPublicRequest);
                ctx.Context.Session.EndResponse();
            }
            catch (Exception ex)
            {
                m_Logger.Error("Process request error: " + ex.Message);
                m_Logger.Error(ex.StackTrace);
            }
        }

    }

    public class CommonServerNode
    {
        public ServerNode Node { get; private set; }

        public ServerSetting InternalSetting { get; private set; }
        public ServerSetting PublicSetting { get; private set; }

        public string NodeName { get; private set; }
        public string GroupName { get; private set; }
        public string StorageName { get; private set; }

        public List<string> ServiceFileNames { get; private set; }

        public CommonServerNode()
        {
            Node = null;

            PublicSetting = null;
            InternalSetting = null;

            NodeName = "";
            GroupName = "";
            StorageName = "";

            ServiceFileNames = new List<string>();
        }

        public bool IsWorking()
        {
            var node = Node;
            if (node == null) return false;
            return node.IsWorking();
        }

        public async Task StartAsync(CommonServerNodeSetting setting, string storageName, IServerLogger logger = null)
        {
            PublicSetting = setting.PublicServerSetting;
            InternalSetting = setting.InternalServerSetting;

            NodeName = setting.NodeName;
            GroupName = setting.GroupName;

            StorageName = storageName;
            if (String.IsNullOrEmpty(StorageName)) StorageName = "";

            var fileNames = setting.Services.Split(',');
            ServiceFileNames.Clear();
            ServiceFileNames.AddRange(fileNames);

            if (Node == null) Node = new ServerNode(NodeName, GroupName, logger);

            if (Node != null && !Node.IsWorking())
            {
                Node.SetServerInfoStorage(StorageName);
                Node.ResetLocalServiceFiles(ServiceFileNames);

                if (String.IsNullOrEmpty(StorageName) || InternalSetting == null)
                {
                    await Node.StartStandaloneMode(PublicSetting);
                }
                else
                {
                    await Node.Start(InternalSetting, PublicSetting);
                }

                await Task.Delay(100);

                if (Node.IsWorking())
                {
                    var svrLogger = Node.GetLogger();
                    if (svrLogger != null)
                    {
                        svrLogger.Info("Server Started - " + NodeName);
                        svrLogger.Info("Custom Routers: " + Node.GetCustomRouterNames());
                        svrLogger.Info("Default Multithreading: " + Node.GetDefaultMultithreading());
                        if (Node.IsStandalone()) svrLogger.Info("Server is in standalone mode");
                        else svrLogger.Info("Internal URL: " + Node.GetInternalAccessUrl());
                        svrLogger.Info("Public URL: " + Node.GetPublicAccessUrl());
                    }
                }
            }
        }

        public async void Start(CommonServerNodeSetting setting, string storageName, IServerLogger logger = null)
        {
            await StartAsync(setting, storageName, logger);
        }

        public async Task StopAsync()
        {
            if (Node != null && Node.IsWorking())
            {
                await Node.Stop();
                await Task.Delay(100);

                if (!Node.IsWorking())
                {
                    var svrLogger = Node.GetLogger();
                    if (svrLogger != null)
                    {
                        svrLogger.Info("Server Stopped - " + NodeName);
                    }
                }
            }
        }

        public async void Stop()
        {
            await StopAsync();
        }
    }

    public class CommonServerContainer
    {
        public string StorageName { get; private set; }

        public Dictionary<string, CommonServerNode> ServerNodes { get; private set; }

        public CommonServerContainer()
        {
            StorageName = "";
            ServerNodes = new Dictionary<string, CommonServerNode>();
        }

        public bool IsWorking()
        {
            var nodes = ServerNodes;
            foreach (var item in nodes)
            {
                var node = item.Value.Node;
                if (node != null)
                {
                    if (node.IsWorking()) return true;
                }
            }
            return false;
        }

        public async Task StopAsync()
        {
            var nodes = ServerNodes;
            foreach (var item in nodes)
            {
                if (item.Value.Node != null)
                {
                    await item.Value.StopAsync();
                }
            }
            await Task.Delay(100);
        }

        public void Stop()
        {
            var nodes = ServerNodes;
            foreach (var item in nodes)
            {
                if (item.Value.Node != null)
                {
                    //Task.Factory.StartNew(async () => await item.Value.StopAsync()).Wait();
                    Task.Factory.StartNew(() => item.Value.Stop()).Wait(); // seems better?

                    Thread.Sleep(100); // make sure the action has been started (necessary?)

                    for (int i = 0; i < 300; i++) // try to wait till stopping is done (max waiting time is 30s)
                    {
                        if (item.Value.Node.IsStopping()) Thread.Sleep(100);
                        else break;
                    }
                }
            }
            Thread.Sleep(100);
        }

        public async Task StartAsync(CommonServerContainerSetting setting, IServerLogger logger = null)
        {
            await StopAsync();

            var storage = String.IsNullOrEmpty(setting.ServerInfoStorage) 
                            ? "" : String.Copy(setting.ServerInfoStorage);

            var nodes = new Dictionary<string, CommonServerNode>();
            foreach(var itemSetting in setting.ServerNodeSettings)
            {
                if (nodes.ContainsKey(itemSetting.NodeName)) continue;

                CommonServerNode node = new CommonServerNode();
                await node.StartAsync(itemSetting, storage, logger);

                nodes.Add(itemSetting.NodeName, node);
            }

            await Task.Delay(100);

            ServerNodes = nodes;
            StorageName = storage;
        }

        public void Start(CommonServerContainerSetting setting, IServerLogger logger = null)
        {
            Stop();

            var storage = String.IsNullOrEmpty(setting.ServerInfoStorage)
                            ? "" : String.Copy(setting.ServerInfoStorage);

            var nodes = new Dictionary<string, CommonServerNode>();
            foreach (var itemSetting in setting.ServerNodeSettings)
            {
                if (nodes.ContainsKey(itemSetting.NodeName)) continue;

                CommonServerNode node = new CommonServerNode();

                //Task.Factory.StartNew(async () => await node.StartAsync(itemSetting, storage, logger)).Wait();
                Task.Factory.StartNew(() => node.Start(itemSetting, storage, logger)).Wait(); // seems better?

                Thread.Sleep(100); // make sure the action has been started (necessary?)

                if (node.Node != null)
                {
                    for (int i = 0; i < 300; i++) // try to wait till starting is done (max waiting time is 30s)
                    {
                        if (node.Node.IsStarting() || node.Node.IsStopping()) Thread.Sleep(100);
                        else break;
                    }
                }

                nodes.Add(itemSetting.NodeName, node);
            }

            Thread.Sleep(100);

            ServerNodes = nodes;
            StorageName = storage;

        }
    }


}
