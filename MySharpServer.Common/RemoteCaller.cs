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
        static RemoteCaller()
        {
            DefaultTimeout = 30 * 1000; // 30 seconds
            HttpConnectionLimit = Environment.ProcessorCount * 8;
        }

        public static int DefaultTimeout { get; set; }

        public static int HttpConnectionLimit
        {
            get { return ServicePointManager.DefaultConnectionLimit; }
            set { ServicePointManager.DefaultConnectionLimit = value; }
        }

        public static async Task<string> Call(string url, string service, string action, string data, string key = "", int timeout = 0)
        {
            string result = "";

            string path = service + "/" + action + (key.Length > 0 ? ("/" + key) : "");

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest
                .Create((url.StartsWith("http") ? url : ("http://" + url)) + (url.EndsWith("/") ? "" : "/") + path);

            httpWebRequest.Accept = "*/*";
            httpWebRequest.UserAgent = "curl/7.50.0";
            httpWebRequest.ContentType = "text/plain";
            httpWebRequest.Method = "POST";

            httpWebRequest.Timeout = timeout > 0 ? timeout : DefaultTimeout;

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

        public static async Task<string> RandomCall(Dictionary<string, List<string>> remoteServers, string service, string action, string data, int timeout = 0)
        {
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
                        return await Call(remoteUrl, service, action, data, svrKey, timeout);
                    }
                }
            }
            return "";
        }
    }
}
