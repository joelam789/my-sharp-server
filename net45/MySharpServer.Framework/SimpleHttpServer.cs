using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using SharpNetwork.Core;
using SharpNetwork.SimpleHttp;

using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class SimpleHttpServer : IWebServer
    {
        protected Server m_Server = null;

        protected string m_Ip = "0.0.0.0"; // any ip
        protected string m_Protocol = "http";
        protected int m_Port = 9991;

        protected IDictionary<string, string> m_HeadersForAllowOrigin = new Dictionary<string, string>();

        public IServerNode RequestHandler { get; private set; }
        public IServerLogger Logger { get; private set; }
        public string AllowOrigin { get; private set; }
        public int Flags { get; private set; }

        public SimpleHttpServer(IServerNode handler, IServerLogger logger = null, int flags = 0, string allowOrigin = "")
        {
            RequestHandler = handler;

            Logger = logger;
            if (Logger == null) Logger = new ConsoleLogger();

            Flags = flags;

            AllowOrigin = "";
            if ((Flags & RequestContext.FLAG_PUBLIC) != 0) AllowOrigin = allowOrigin; // normally only public services need this
            if (AllowOrigin.Length > 0)
            {
                m_HeadersForAllowOrigin.Add("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Accept");
                m_HeadersForAllowOrigin.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS, PUT, HEAD, DELETE, CONNECT");
                m_HeadersForAllowOrigin.Add("Access-Control-Allow-Origin", AllowOrigin);
            }

            if (HttpMessage.JsonCodec == HttpMessage.DefaultJsonCodec)
                HttpMessage.JsonCodec = new CommonJsonCodec();
        }

        public bool Start(int port = 0, string ipstr = "", string certFile = "", string certKey = "")
        {
            Stop();

            if (m_Server == null)
            {
                m_Server = new Server();
                m_Server.SetIoFilter(new MessageCodec(AllowOrigin.Length > 0 ? m_HeadersForAllowOrigin : null));
                m_Server.SetIoHandler(new SimpleHttpNetworkEventHandler(this));

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

                            m_Protocol = "https";

                            Console.WriteLine("Loaded cert: " + certFilepath);
                            Logger.Info("Loaded cert: " + certFilepath);
                        }
                        else
                        {
                            Console.WriteLine("Cert file not found: " + certFilepath);
                            Logger.Error("Cert file not found: " + certFilepath);
                        }
                    }
                    else m_Protocol = "http";
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
                m_Server.SetIdleTime(Session.IO_BOTH, 5 * 60); // set max idle time to 5 mins
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

    public class SimpleHttpNetworkEventHandler : NetworkEventHandler
    {
        SimpleHttpServer m_SimpleHttpServer = null;

        public SimpleHttpNetworkEventHandler(SimpleHttpServer server) : base()
        {
            m_SimpleHttpServer = server;
        }

        public override void OnConnect(Session session)
        {
            base.OnConnect(session);
        }

        public override void OnDisconnect(Session session)
        {
            base.OnDisconnect(session);
        }

        public override void OnError(Session session, int errortype, Exception error)
        {
            base.OnError(session, errortype, error);

            if (Session.IsProcessError(errortype)) m_SimpleHttpServer.Logger.Error(error.ToString());
            else m_SimpleHttpServer.Logger.Error(error.Message);

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

            HttpMessage msg = ctx.Data as HttpMessage;
            if (msg == null || !msg.IsString())
            {
                ctx.Session.Close();
                return;
            }
            else
            {
                string content = msg.RequestUrl;

                if ((m_SimpleHttpServer.Flags & RequestContext.FLAG_PUBLIC) != 0 
                    && (m_SimpleHttpServer.Flags & RequestContext.FLAG_ALLOW_PARENT_PATH) != 0)
                {
                    var usefulPath = new List<string>();
                    var parts = content.Split('/');
                    for (int i = parts.Length - 1; i >= 0; i--)
                    {
                        var part = parts[i].Trim();
                        if (part.Length > 0) usefulPath.Add(part);
                        if (usefulPath.Count >= 2)
                        {
                            content = "/" + usefulPath[1] + "/" + usefulPath[0];
                            break;
                        }
                    }
                }

                content += (content.EndsWith("/") ? "" : "/") + msg.MessageContent;
                var reqctx = new RequestContext(new SimpleHttpSession(ctx.Session), content, m_SimpleHttpServer.Flags);
                reqctx.RequestPath = msg.RequestUrl;
                reqctx.Headers = new Dictionary<string, string>(msg.Headers);
                m_SimpleHttpServer.RequestHandler.HandleRequest(reqctx);
            }


        }
    }
}
