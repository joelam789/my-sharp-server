using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;

using SharpNetwork.Core;
using SharpNetwork.SimpleWebSocket;

using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class WebSocketSession: IWebSession
    {
        private Session m_Session = null;

        private string m_Protocol = "";
        private string m_RemoteAddress = "";

        public WebSocketSession(Session session)
        {
            m_Session = session;
            GetRemoteAddress();
            GetProtocol();
        }

        public Session GetSocketSession()
        {
            return m_Session;
        }

        public string GetGroup()
        {
            return m_Session == null || m_Session.UserData == null ? "" : m_Session.UserData.ToString();
        }

        public string GetProtocol()
        {
            if (m_Protocol.Length <= 0)
            {
                if (m_Session != null)
                {
                    if (m_Session.GetStream() is SslStream) m_Protocol = "wss";
                    else m_Protocol = "ws";
                }
            }
            return m_Protocol;
        }

        public string GetRemoteAddress()
        {
            if (m_RemoteAddress.Length <= 0)
            {
                if (m_Session != null)
                {
                    m_RemoteAddress = m_Session.GetRemoteIp() + ":" + m_Session.GetRemotePort();
                }
            }
            return m_RemoteAddress;
        }

        public async Task Send(string msg)
        {
           await Task.Run(() =>
           {
               if (m_Session != null)
               {
                   m_Session.Send(new WebMessage(msg));
               }
           });
        }

        public void BeginResponse()
        {
            // do nothing ...
        }

        public void EndResponse()
        {
            // do nothing ...
        }

        public void CloseConnection()
        {
            if (m_Session != null)
            {
                m_Session.Close();
            }
        }
    }
}
