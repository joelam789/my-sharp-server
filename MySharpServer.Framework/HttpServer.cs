﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class HttpServer: IWebServer
    {
        protected string m_Ip = "0.0.0.0"; // any ip
        protected string m_Protocol = "http";
        protected int m_Port = 9991;
        protected int m_Flags = 0;
        protected HttpListener m_Server = null;
        protected Thread m_ListenerThread = null;

        protected IServerNode m_RequestHandler = null;
        protected IServerLogger m_Logger = null;

        protected string m_AllowOrigin = "";

        protected ConcurrentExclusiveSchedulerPair m_TaskSchedulerPair = null;
        protected TaskFactory m_ListenerTaskFactory = null;


        public HttpServer(IServerNode handler, IServerLogger logger = null, int flags = 0, string allowOrigin = "")
        {
            m_TaskSchedulerPair = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, Environment.ProcessorCount * 2);
            m_ListenerTaskFactory = new TaskFactory(m_TaskSchedulerPair.ConcurrentScheduler);

            m_RequestHandler = handler;

            m_Logger = logger;
            if (m_Logger == null) m_Logger = new ConsoleLogger();

            m_Flags = flags;

            if ((m_Flags & RequestContext.FLAG_PUBLIC) != 0) m_AllowOrigin = allowOrigin; // normally only public services need this
        }

        public bool Start(int port = 0, string ipstr = "", string certFile = "", string certKey = "")
        {
            if (m_Server != null) Stop();

            if (ipstr.Length > 0) m_Ip = ipstr;
            else m_Ip = "0.0.0.0"; // any ip

            if (port >= 0) m_Port = port;

            bool isServerOK = true;

            if (isServerOK && m_Server == null && m_Port > 0)
            {
                isServerOK = false;

                try
                {
                    m_Server = new HttpListener();

                    // if need ssl then just do NOT let "certFile" be empty, may set it with "https"
                    if (certFile.Length > 0) m_Protocol = "https";
                    else m_Protocol = "http";

                    if (m_Ip.Length > 0 && m_Ip != "0.0.0.0")
                    {
                        var uri = new Uri(m_Protocol + "://" + m_Ip + ":" + m_Port, UriKind.Absolute);
                        m_Server.Prefixes.Add(uri.AbsoluteUri);
                    }
                    else
                    {
                        m_Server.Prefixes.Add(String.Format(@"{0}://+:{1}/", m_Protocol, m_Port));
                    }

                    if (certFile.Length > 0)
                    {
                        //m_Server.HttpsCert = new X509Certificate(certFile, certKey);

                        //var cert = new X509Certificate2(certFile, certKey);
                        //X509Store store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                        //store.Open(OpenFlags.ReadWrite);
                        //if (!store.Certificates.Contains(cert)) store.Add(cert);
                        //store.Close();

                        // ref - http://stackoverflow.com/questions/11403333/httplistener-with-https-support
                        //     - http://stackoverflow.com/questions/21629395/http-listener-with-https-support-coded-in-c-sharp
                    }

                    m_Server.Start();

                    m_ListenerThread = new Thread(HandleHttpRequests);
                    m_ListenerThread.Start();

                    isServerOK = true;

                }
                catch (Exception ex)
                {
                    try
                    {
                        Stop();
                    }
                    catch { }
                    m_Server = null;
                    m_Logger.Error("HTTP listening error: " + ex.Message);
                }
            }

            isServerOK = isServerOK && IsWorking();

            if (!isServerOK)
            {
                try
                {
                    Stop();
                }
                catch { }
                m_Server = null;
                m_Logger.Error("Failed to start server.");
            }

            return isServerOK;
        }

        private void HandleHttpRequests()
        {
            while (m_Server.IsListening)
            {
                try
                {
                    var context = m_Server.BeginGetContext(new AsyncCallback(ListenerCallback), m_Server);
                    context.AsyncWaitHandle.WaitOne();
                }
                catch { }
            }
        }

        private async void ListenerCallback(IAsyncResult ar)
        {
            HttpListener listener = null;
            HttpListenerContext context = null;

            string remoteIp = "";

            try
            {
                listener = ar.AsyncState as HttpListener;
                context = listener.EndGetContext(ar);
                remoteIp = context.Request.RemoteEndPoint.Address.ToString();
            }
            catch (Exception ex)
            {
                m_Logger.Error("HTTP context error: " + ex.Message);
                return;
            }

            try
            {
                if (context != null && remoteIp.Length > 0)
                {
                    //Task.Factory.StartNew((param) => ProcessData(param), context);
                    //await ProcessData(context);
                    await m_ListenerTaskFactory.StartNew((param) => ProcessData(param), context).ConfigureAwait(false);
                }
                else if (context != null)
                {
                    context.Response.Close();
                    //context.Response.Abort(); // make sure the connection is closed
                }
            }
            catch (Exception ex)
            {
                m_Logger.Error("HTTP process error: " + ex.Message);
            }
        }

        protected virtual async Task ProcessData(object obj)
        {
            HttpListenerContext ctx = obj as HttpListenerContext;
            if (ctx == null) return;
            if (m_RequestHandler == null) return;
            try
            {
                var request = ctx.Request;
                string content = request.Url.AbsolutePath;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    content += (content.EndsWith("/") ? "" : "/") + (await reader.ReadToEndAsync());
                }
                await m_RequestHandler.HandleRequest(new RequestContext(new HttpSession(ctx, m_AllowOrigin), content, m_Flags));
            }
            catch (Exception ex)
            {
                m_Logger.Error("HTTP context error: " + ex.Message);
            }
        }

        public void Stop()
        {
            try
            {
                if (m_Server != null && m_Server.IsListening)
                {
                    m_Server.Stop();
                    if (m_ListenerThread != null)
                    {
                        m_ListenerThread.Join();
                        m_ListenerThread = null;
                    }
                    m_Server.Close();
                }
                if (m_ListenerThread != null)
                {
                    m_ListenerThread.Join();
                    m_ListenerThread = null;
                }
                m_Server = null;
            }
            catch { }
        }

        public bool IsWorking()
        {
            return m_Server != null && m_Server.IsListening;
        }

        public string GetProtocol()
        {
            return m_Protocol;
        }

        public string GetIp()
        {
            return m_Ip;
        }

        public int GetPort()
        {
            return m_Port;
        }

        public int GetClientCount()
        {
            return 0; // not supported
        }

        public Dictionary<string, IWebSession> GetClients()
        {
            return null; // not supported
        }

        public void GroupClient(string client, string group)
        {
            return; // not supported grouping by http/https
        }

        public void BroadcastToGroup(string msg, string group)
        {
            return; // not supported broadcasting by http/https
        }

        public void Broadcast(string msg, List<string> clients = null)
        {
            return; // not supported broadcasting by http/https
        }
    }
}
