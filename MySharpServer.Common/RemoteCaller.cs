using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Threading.Tasks;

namespace MySharpServer.Common
{
    public static class RemoteCaller
    {
        public static async Task<string> Call(string url, string service, string action, string data, string key = "", int timeout = 0)
        {
            string result = "";

            string path = service + "/" + action + (key.Length > 0 ? ("/" + key) : "");

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest
                .Create((url.StartsWith("http") ? url : ("http://" + url)) + (url.EndsWith("/") ? "" : "/") + path);

            httpWebRequest.ContentType = "text/plain";
            httpWebRequest.Method = "POST";

            if (timeout > 0) httpWebRequest.Timeout = timeout;

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                await streamWriter.WriteAsync(data);
                await streamWriter.FlushAsync();
                streamWriter.Close();
            }

            using (var response = await httpWebRequest.GetResponseAsync())
            {
                using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                {
                    result = await streamReader.ReadToEndAsync();
                    streamReader.Close();
                }
            }

            return result;
        }

        public static async Task<string> GroupClient(string server, string key, string client, string group, int timeout = 0)
        {
            string service = "network";
            string action = "group-client";

            string data = group + "|" + client;

            return await Call(server, service, action, data, key, timeout);
        }

        public static async Task<string> BroadcastToGroup(string server, string key, string msg, string group, int timeout = 0)
        {
            string service = "network";
            string action = "broadcast-to-group";

            string data = group + "|" + msg;

            return await Call(server, service, action, data, key, timeout);
        }

        public static async Task<string> Broadcast(string server, string key, string msg, List<string> clients = null, int timeout = 0)
        {
            string service = "network";
            string action = "broadcast";

            string data = (clients != null && clients.Count > 0 ? String.Join(",", clients.ToArray()) : "") + "|" + msg;

            return await Call(server, service, action, data, key, timeout);
        }

        public static async Task<string> RandomCall(Dictionary<string, List<string>> remoteServers, string service, string action, string data)
        {
            string result = "";
            List<string> remoteServerList = null;
            if (remoteServers != null && remoteServers.TryGetValue(service, out remoteServerList))
            {
                if (remoteServerList != null && remoteServerList.Count > 0)
                {
                    var remoteInfoParts = RandomPicker.Pick<string>(remoteServerList).Split('|');
                    if (remoteInfoParts.Length >= 2)
                    {
                        string remoteUrl = remoteInfoParts[1]; // name | url | key
                        string svrKey = remoteInfoParts.Length >= 3 ? remoteInfoParts[2] : "";
                        try
                        {
                            result = await Call(remoteUrl, service, action, data, svrKey);
                        }
                        catch
                        {
                            result = "";
                        }
                    }
                }
            }
            return result;
        }
    }
}
