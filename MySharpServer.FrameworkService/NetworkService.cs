using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MySharpServer.Common;

namespace MySharpServer.FrameworkService
{
    [Access(Name = "network", IsPublic = false)]
    public class NetworkService
    {
        private IWebServer m_Server = null;

        [Access(Name = "set-server")]
        public string SetServer(IWebServer server)
        {
            m_Server = server;
            if (m_Server != null) Console.WriteLine("Bind server to network service: " + m_Server.GetProtocol() + " on " + m_Server.GetPort());
            return "";
        }

        [Access(Name = "get-server")]
        public IWebServer GetServer(string param)
        {
            return m_Server;
        }

        [Access(Name = "group-client")]
        public string GroupClient(RequestContext ctx)
        {
            var server = m_Server;
            if (server == null || !server.IsWorking()) return "";

            var parts = ctx.Data.ToString().Split('|'); // group|client

            if (parts != null && parts.Length >= 2)
            {
                string group = parts[0];
                string client = parts[1];
                server.GroupClient(client, group);
                return "ok";
            }
            return "";
        }

        [Access(Name = "broadcast-to-group")]
        public string BroadcastToGroup(RequestContext ctx)
        {
            var server = m_Server;
            if (server == null || !server.IsWorking()) return "";

            string msg = "";
            string group = "";

            var content = ctx.Data.ToString(); // group|msg
            int pos = content.IndexOf('|');
            if (pos >= 0)
            {
                group = content.Substring(0, pos);
                msg = content.Substring(pos + 1);
            }
            if (group.Length > 0 && msg.Length > 0)
            {
                server.BroadcastToGroup(msg, group);
                return "ok";
            }
            return "";
        }

        [Access(Name = "broadcast")]
        public string Broadcast(RequestContext ctx)
        {
            var server = m_Server;
            if (server == null || !server.IsWorking()) return "";

            string msg = "";
            string clients = "";
            
            var content = ctx.Data.ToString(); // client1,client2,client3|msg
            int pos = content.IndexOf('|');
            if (pos >= 0)
            {
                clients = content.Substring(0, pos);
                msg = content.Substring(pos + 1);
            }

            if (!String.IsNullOrEmpty(clients) && msg.Length > 0)
            {
                List<string> list = new List<string>();
                list.AddRange(clients.Split(','));
                server.Broadcast(msg, list);
                return "ok";
            }
            else if (msg.Length > 0)
            {
                server.Broadcast(msg);
                return "ok";
            }
            return "";
        }
    }
}
