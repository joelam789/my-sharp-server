﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using SharpNetwork;
using SharpNetwork.SimpleWebSocket;

using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class WebSocketServer : IWebServer
    {
        protected Server m_Server = null;

        protected string m_Ip = "0.0.0.0"; // any ip
        protected string m_Protocol = "ws";
        protected int m_Port = 9310;

        protected int m_ClientCount = 0;

        protected Dictionary<string, Session> m_ClientSessions = new Dictionary<string, Session>();
        protected Dictionary<string, List<Session>> m_ClientGroups = new Dictionary<string, List<Session>>();

        public IServerNode RequestHandler { get; private set; }

        public IServerLogger Logger { get; private set; }

        public int Flags { get; private set; }

        public WebSocketServer(IServerNode handler, IServerLogger logger = null, int flags = 0)
        {
            RequestHandler = handler;

            Logger = logger;
            if (Logger == null) Logger = new ConsoleLogger();

            Flags = flags;
        }

        public string AddClientSession(Session session)
        {
            var sessionId = session.GetRemoteIp() + ":" + session.GetRemotePort();
            lock (m_ClientSessions)
            {
                if (m_ClientSessions.ContainsKey(sessionId)) m_ClientSessions.Remove(sessionId);
                m_ClientSessions.Add(sessionId, session);
                m_ClientCount = m_ClientSessions.Count;
            }
            return sessionId;
        }

        public string RemoveClientSession(Session session)
        {
            var sessionId = session.GetRemoteIp() + ":" + session.GetRemotePort();
            var groupName = session.UserData == null ? "" : session.UserData.ToString();
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
                    if (certFile != null && certFile.Length > 0 && File.Exists(certFile))
                    {
                        if (certKey == null) m_Server.SetCert(new X509Certificate2(certFile));
                        else m_Server.SetCert(new X509Certificate2(certFile, certKey));
                        m_Protocol = "wss";
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

        public void GroupClient(string client, string group)
        {
            lock (m_ClientSessions)
            {
                Session session = null;
                if (m_ClientSessions.TryGetValue(client, out session))
                {
                    List<Session> list = null;
                    lock (m_ClientGroups)
                    {
                        if (!m_ClientGroups.TryGetValue(group, out list))
                        {
                            list = new List<Session>();
                            m_ClientGroups.Add(group, list);
                        }
                        if (list != null && session != null)
                        {
                            session.UserData = group;
                            if (!list.Contains(session)) list.Add(session);
                        }
                    }
                }
            }
        }
        public void BroadcastToGroup(string msg, string group)
        {
            List<Session> list = null;
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
                    session.Send(new WebMessage(msg));
                }
            }
        }
        public void Broadcast(string msg, List<string> clients = null)
        {
            List<Session> list = new List<Session>();
            lock (m_ClientSessions)
            {
                if (clients != null)
                {
                    foreach (var item in clients)
                    {
                        Session session = null;
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
                    session.Send(new WebMessage(msg));
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
                var clientId = m_WebSocketServer.AddClientSession(session);
                var info = m_WebSocketServer.RequestHandler.GetName() + "@" + m_WebSocketServer.RequestHandler.GetGroup() + "|" + clientId;
                m_WebSocketServer.RequestHandler.EmitLocalEvent("on-connect", info);
            }
        }

        public override void OnDisconnect(Session session)
        {
            if (m_WebSocketServer.IsWorking())
            {
                var clientId = m_WebSocketServer.RemoveClientSession(session);
                var info = m_WebSocketServer.RequestHandler.GetName() + "@" + m_WebSocketServer.RequestHandler.GetGroup() + "|" + clientId;
                m_WebSocketServer.RequestHandler.EmitLocalEvent("on-disconnect", info);
            }

            base.OnDisconnect(session);
        }

        public override void OnError(Session session, int errortype, string errormsg)
        {
            base.OnError(session, errortype, errormsg);

            if (errormsg != null && errormsg.Length > 0) m_WebSocketServer.Logger.Error(errormsg);
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
                m_WebSocketServer.RequestHandler.HandleRequest(new RequestContext(new WebSocketSession(ctx.Session), msg.MessageContent, m_WebSocketServer.Flags));
            }

            
        }
    }
}