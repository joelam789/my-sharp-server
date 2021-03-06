﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class HttpSession : IWebSession
    {
        private HttpListenerContext m_Session = null;

        private string m_Protocol = "";
        private string m_RemoteAddress = "";

        private string m_AllowOrigin = "";

        private bool m_IsConnected = true;

        public HttpSession(HttpListenerContext session, string allowOrigin = "")
        {
            m_Session = session;
            m_AllowOrigin = allowOrigin;

            GetRemoteAddress();
            GetProtocol();
        }

        public string GetGroup()
        {
            return "";
        }

        public string GetProtocol()
        {
            if (m_Protocol.Length <= 0)
            {
                if (m_Session != null && m_Session.Request != null && m_Session.Request.InputStream != null)
                {
                    if (m_Session.Request.InputStream is SslStream) m_Protocol = "https";
                    else m_Protocol = "http";
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
                    if (m_Session != null && m_Session.Request != null)
                    {
                        var remoteEndPoint = m_Session.Request.RemoteEndPoint;
                        addr = remoteEndPoint.Address.ToString() + ":" + remoteEndPoint.Port;
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
                url = m_Session.Request.RawUrl;
            }
            return url;
        }

        public void BeginResponse()
        {
            if (m_Session != null)
            {
                if (m_AllowOrigin != null && m_AllowOrigin.Length > 0)
                {
                    try
                    {
                        m_Session.Response.AppendHeader("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Accept");
                        m_Session.Response.AppendHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS, PUT, HEAD, DELETE, CONNECT");
                        m_Session.Response.AppendHeader("Access-Control-Allow-Origin", m_AllowOrigin);
                    }
                    catch { }
                }
            }
        }

        public async Task Send(string msg, IDictionary<string, string> metadata = null, int httpStatusCode = 0, string httpReasonPhrase = null)
        {
            if (m_Session != null)
            {
                if (metadata != null && metadata.Count > 0)
                {
                    foreach (var item in metadata) m_Session.Response.AppendHeader(item.Key, item.Value);
                }
                if (httpStatusCode > 0 && httpReasonPhrase != null)
                {
                    m_Session.Response.StatusCode = httpStatusCode;
                    m_Session.Response.StatusDescription = httpReasonPhrase;
                }
                byte[] buffer = Encoding.UTF8.GetBytes(msg);
                await m_Session.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            }
        }

        public void EndResponse()
        {
            if (m_Session != null)
            {
                try { m_Session.Response.OutputStream.Close(); }
                catch { }
                try { m_Session.Response.Close(); }
                catch { }
            }
        }

        public bool IsConnected()
        {
            return m_IsConnected;
        }

        public void CloseConnection()
        {
            if (m_Session != null)
            {
                try { m_Session.Response.Abort(); }
                catch { }
            }
            m_IsConnected = false;
        }
    }
}
