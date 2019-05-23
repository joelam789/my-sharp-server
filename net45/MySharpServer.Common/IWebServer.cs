using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySharpServer.Common
{
    public interface IWebServer
    {
        bool Start(int port = 0, string ipstr = "", string certFile = "", string certKey = "");
        bool IsWorking();
        string GetProtocol();
        string GetIp();
        int GetPort();
        void Stop();

        int GetClientCount();
        Dictionary<string, IWebSession> GetClients();

        void GroupClient(string client, string group);
        void BroadcastToGroup(string msg, string group);
        void Broadcast(string msg, List<string> clients = null);
    }
}
