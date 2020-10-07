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

        private bool m_IsConnected = true;

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
                var addr = "";
                try
                {
                    if (m_Session != null)
                    {
                        addr = m_Session.GetRemoteIp() + ":" + m_Session.GetRemotePort();
                    }
                }
                catch { }
                if (!string.IsNullOrEmpty(addr)) m_RemoteAddress = addr;
            }
            return m_RemoteAddress;
        }

        public string GetRequestPath()
        {
            var url = "";
            if (m_Session != null)
            {
                url = WebMessage.GetSessionData(m_Session, "Path").ToString();
            }
            return url;
        }

        public async Task Send(string msg, IDictionary<string, string> metadata = null, int httpStatusCode = 0, string httpReasonPhrase = null)
        {
           await Task.Run(() =>
           {
               if (m_Session != null)
               {
                   m_Session.Send(new WebMessage(msg));
               }
           }).ConfigureAwait(false);
        }

        public void BeginResponse()
        {
            // do nothing ...
        }

        public void EndResponse()
        {
            // do nothing ...
        }

        public bool IsConnected()
        {
            if (m_IsConnected)
            {
                if (m_Session != null)
                {
                    return m_Session.GetState() == 1;
                }
            }
            return false;
        }

        public void CloseConnection()
        {
            if (m_Session != null)
            {
                m_Session.Close(false);
            }
            m_IsConnected = false;
        }
    }
}
