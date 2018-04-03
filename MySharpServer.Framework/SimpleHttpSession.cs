﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;

using SharpNetwork.Core;
using SharpNetwork.SimpleHttp;

using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class SimpleHttpSession : IWebSession
    {
        private Session m_Session = null;

        private string m_Protocol = "";
        private string m_RemoteAddress = "";

        private bool m_StartedResponse = false;

        public SimpleHttpSession(Session session)
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
            return "";
        }

        public string GetProtocol()
        {
            if (m_Protocol.Length <= 0)
            {
                if (m_Session != null)
                {
                    if (m_Session.GetStream() is SslStream) m_Protocol = "https";
                    else m_Protocol = "http";
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

        public async Task Send(string msg, IDictionary<string, string> metadata = null)
        {
            await Task.Run(() =>
            {
                if (m_Session != null)
                {
                    HttpMessage.Send(m_Session, msg, metadata);
                }
            }).ConfigureAwait(false);
        }

        public void BeginResponse()
        {
            m_StartedResponse = true;
        }

        public void EndResponse()
        {
            if (!m_StartedResponse)
            {
                if (m_Session != null)
                {
                    m_StartedResponse = true;
                    HttpMessage.Send(m_Session, "");
                }
            }
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