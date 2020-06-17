using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using SharpNetwork.Core;
using SharpNetwork.SimpleWebSocket;

using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class WebSocketServer : IWebServer
    {
        protected Server m_Server = null;

        protected string m_Ip = "0.0.0.0"; // any ip
        protected string m_Protocol = "ws";
        protected int m_Port = 9992;

        protected int m_ClientCount = 0;

        protected Dictionary<string, IWebSession> m_ClientSessions = new Dictionary<string, IWebSession>();
        protected Dictionary<string, List<IWebSession>> m_ClientGroups = new Dictionary<string, List<IWebSession>>();

        public IServerNode RequestHandler { get; private set; }
        public IServerLogger Logger { get; private set; }

        public int Flags { get; private set; }

        public WebSocketServer(IServerNode handler, IServerLogger logger = null, int flags = 0)
        {
            RequestHandler = handler;

            Logger = logger;
            if (Logger == null) Logger = new ConsoleLogger();

            Flags = flags;

            if (WebMessage.JsonCodec == WebMessage.DefaultJsonCodec)
                WebMessage.JsonCodec = new CommonJsonCodec();
        }

        public string AddClientSession(WebSocketSession session)
        {
            var sessionId = session.GetRemoteAddress();
            if (String.IsNullOrEmpty(sessionId)) return "";
            lock (m_ClientSessions)
            {
                if (m_ClientSessions.ContainsKey(sessionId)) m_ClientSessions.Remove(sessionId);
                m_ClientSessions.Add(sessionId, session);
                m_ClientCount = m_ClientSessions.Count;
            }
            return sessionId;
        }

        public string RemoveClientSession(WebSocketSession session)
        {
            var sessionId = session == null ? "" : session.GetRemoteAddress();
            if (String.IsNullOrEmpty(sessionId)) return "";
            var groupName = session.GetSocketSession().UserData == null ? "" : session.GetSocketSession().UserData.ToString();
            if (groupName.Length > 0)
            {
                lock (m_ClientGroups)
                {
                    if (m_ClientGroups.ContainsKey(groupName))
                    {
                        m_ClientGroups[groupName].Remove(session);
                    }
                }
            }
            lock (m_ClientSessions)
            {
                if (m_ClientSessions.ContainsKey(sessionId)) m_ClientSessions.Remove(sessionId);
                m_ClientCount = m_ClientSessions.Count;
            }
            return sessionId + (groupName.Length > 0 ? ("@" + groupName) : "");
        }

        public WebSocketSession FindClientSession(string sessionId)
        {
            if (String.IsNullOrEmpty(sessionId)) return null;
            IWebSession session = null;
            lock (m_ClientSessions)
            {
                if (!m_ClientSessions.TryGetValue(sessionId, out session)) session = null;
            }
            return session == null ? null : session as WebSocketSession;
        }

        public string RemoveClientSession(string sessionId)
        {
            WebSocketSession session = FindClientSession(sessionId);
            return RemoveClientSession(session);
        }

        public bool Start(int port = 0, string ipstr = "", string certFile = "", string certKey = "")
        {
            Stop();

            if (m_Server == null)
            {
                m_Server = new Server();
                m_Server.SetIoFilter(new MessageCodec());
                m_Server.SetIoHandler(new WebSocketNetworkEventHandler(this));

                try
                {
                    string certFilepath = certFile == null ? "" : String.Copy(certFile).Trim();

                    if (certFilepath.Length > 0)
                    {
                        certFilepath = certFilepath.Replace('\\', '/');
                        if (certFilepath[0] != '/' && certFilepath.IndexOf(":/") != 1) // if it is not abs path
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
                            if (folder != null && folder.Length > 0) certFilepath = folder.Replace('\\', '/') + "/" + certFilepath;
                        }

                        //Console.WriteLine("Try to load cert: " + certFilepath);
                        //Logger.Info("Try to load cert: " + certFilepath);

                        if (File.Exists(certFilepath))
                        {
                            if (certKey == null) m_Server.SetCert(new X509Certificate2(certFilepath));
                            else m_Server.SetCert(new X509Certificate2(certFilepath, certKey));

                            m_Protocol = "wss";

                            Console.WriteLine("Loaded cert: " + certFilepath);
                            Logger.Info("Loaded cert: " + certFilepath);
                        }
                        else
                        {
                            Console.WriteLine("Cert file not found: " + certFilepath);
                            Logger.Error("Cert file not found: " + certFilepath);
                        }
                    }
                    else m_Protocol = "ws";
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to setup SSL: " + ex.Message);
                    Logger.Error("Failed to setup SSL: " + ex.Message);
                }
            }

            if (ipstr.Length > 0) m_Ip = ipstr;
            else m_Ip = "0.0.0.0"; // any ip

            if (port >= 0) m_Port = port;

            if (m_Server != null)
            {
                //m_Server.SetIdleTime(Session.IO_BOTH, 3 * 60); // set max idle time to 3 mins
                if (m_Ip.Length > 0 && m_Ip != "0.0.0.0") return m_Server.Start(m_Ip, m_Port);
                else return m_Server.Start(m_Port);
            }

            return false;


        }

        public bool IsWorking()
        {
            return m_Server != null && m_Server.GetState() > 0;
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
        public void Stop()
        {
            if (m_Server != null)
            {
                m_Server.Stop();
                m_Server = null;
            }
            m_ClientCount = 0;
        }

        public int GetClientCount()
        {
            return IsWorking() ? m_ClientCount : 0;
        }

        public Dictionary<string, IWebSession> GetClients()
        {
            Dictionary<string, IWebSession> clients = null;
            lock (m_ClientSessions)
            {
                clients = new Dictionary<string, IWebSession>(m_ClientSessions);
            }
            return clients;
        }

        public void GroupClient(string client, string group)
        {
            lock (m_ClientSessions)
            {
                IWebSession session = null;
                if (m_ClientSessions.TryGetValue(client, out session))
                {
                    List<IWebSession> list = null;
                    lock (m_ClientGroups)
                    {
                        if (!m_ClientGroups.TryGetValue(group, out list))
                        {
                            list = new List<IWebSession>();
                            m_ClientGroups.Add(group, list);
                        }
                        if (list != null && session != null)
                        {
                            (session as WebSocketSession).GetSocketSession().UserData = group;
                            if (!list.Contains(session)) list.Add(session);
                        }
                    }
                }
            }
        }
        public void BroadcastToGroup(string msg, string group)
        {
            List<IWebSession> list = null;
            lock (m_ClientGroups)
            {
                if (!m_ClientGroups.TryGetValue(group, out list))
                {
                    list = null;
                }
            }
            if (list != null)
            {
                foreach (var session in list)
                {
                    session.Send(msg);
                }
            }
        }
        public void Broadcast(string msg, List<string> clients = null)
        {
            List<IWebSession> list = new List<IWebSession>();
            lock (m_ClientSessions)
            {
                if (clients != null)
                {
                    foreach (var item in clients)
                    {
                        IWebSession session = null;
                        if (m_ClientSessions.TryGetValue(item, out session))
                        {
                            list.Add(session);
                        }
                    }
                }
                else
                {
                    foreach (var item in m_ClientSessions)
                    {
                        list.Add(item.Value);
                    }
                }
            }
            if (list != null)
            {
                foreach (var session in list)
                {
                    session.Send(msg);
                }
            }
        }
    }

    public class WebSocketNetworkEventHandler : NetworkEventHandler
    {
        WebSocketServer m_WebSocketServer = null;

        public WebSocketNetworkEventHandler(WebSocketServer server): base()
        {
            m_WebSocketServer = server;
        }

        public override void OnConnect(Session session)
        {
            base.OnConnect(session);
        }

        public override void OnHandshake(Session session)
        {
            base.OnHandshake(session);

            if (m_WebSocketServer.IsWorking())
            {
                var clientSession = new WebSocketSession(session);
                var clientId = m_WebSocketServer.AddClientSession(clientSession);
                //var info = m_WebSocketServer.RequestHandler.GetName() + "@" + m_WebSocketServer.RequestHandler.GetGroup() + "|" + clientId;
                try
                {
                    m_WebSocketServer.RequestHandler.EmitLocalEvent("on-connect", clientSession);
                }
                catch { }

            }
        }

        public override void OnDisconnect(Session session)
        {
            if (m_WebSocketServer.IsWorking())
            {
                //var clientId = m_WebSocketServer.RemoveClientSession(session.GetRemoteIp() + ":" + session.GetRemotePort());
                //var info = m_WebSocketServer.RequestHandler.GetName() + "@" + m_WebSocketServer.RequestHandler.GetGroup() + "|" + clientId;
                var clientId = session.GetRemoteIp() + ":" + session.GetRemotePort();
                var clientSession = m_WebSocketServer.FindClientSession(clientId);

                try
                {
                    m_WebSocketServer.RequestHandler.EmitLocalEvent("on-disconnect", clientSession);
                }
                catch { }

                m_WebSocketServer.RemoveClientSession(clientSession);
            }

            base.OnDisconnect(session);
        }

        public override void OnError(Session session, int errortype, Exception error)
        {
            base.OnError(session, errortype, error);

            if (Session.IsProcessError(errortype)) m_WebSocketServer.Logger.Error(error.ToString());
            else m_WebSocketServer.Logger.Error(error.Message);
        }

        //public override int OnSend(Session session, object data)
        //{
        //    int result = base.OnSend(session, data);
        //    m_WebSocketServer.Logger.Info("On Send - " + data.ToString());
        //    return result;
        //}

        protected override void ProcessMessage(SessionContext ctx)
        {
            if (ctx == null) return;

            WebMessage msg = ctx.Data as WebMessage;
            if (msg == null || !msg.IsString())
            {
                ctx.Session.Close();
                return;
            }
            else
            {
                var reqctx = new RequestContext(new WebSocketSession(ctx.Session), msg.MessageContent, m_WebSocketServer.Flags);
                reqctx.RequestPath = WebMessage.GetSessionData(ctx.Session, "Path").ToString();
                reqctx.Headers = new Dictionary<string, string>(msg.Headers);
                m_WebSocketServer.RequestHandler.HandleRequest(reqctx);
            }

            
        }
    }

    public class CommonJsonCodec : ICommonJsonCodec
    {
        private NewtonJsonCodec m_JsonCodec = new NewtonJsonCodec();

        public string ToJsonString(object obj)
        {
            return m_JsonCodec.ToJsonString(obj);
        }

        public T ToJsonObject<T>(string str) where T : class
        {
            return m_JsonCodec.ToJsonObject<T>(str);
        }

    }
}
