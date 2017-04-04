using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;

namespace MySharpServer.Common
{
    public static class RemoteCaller
    {
        public static string Call(string url, string service, string action, string data, string key = "", int timeout = 0)
        {
            string result = "";

            string path = service + "/" + action + (key.Length > 0 ? ("/" + key) : "");

            HttpWebRequest httpWebRequest = (HttpWebRequest)HttpWebRequest
                .Create((url.StartsWith("http") ? url : ("http://" + url)) + (url.EndsWith("/") ? "" : "/") + path);

            httpWebRequest.ContentType = "text/plain";
            httpWebRequest.Method = "POST";

            if (timeout > 0) httpWebRequest.Timeout = timeout;

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                streamWriter.Write(data);
                streamWriter.Flush();
                streamWriter.Close();
            }

            HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse();
            using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
                streamReader.Close();
            }
            response.Close();

            return result;
        }

        public static string GroupClient(string server, string key, string client, string group, int timeout = 0)
        {
            string service = "network";
            string action = "group";

            string data = group + "|" + client;

            return Call(server, service, action, data, key, timeout);
        }

        public static string BroadcastToGroup(string server, string key, string msg, string group, int timeout = 0)
        {
            string service = "network";
            string action = "group-broadcast";

            string data = group + "|" + msg;

            return Call(server, service, action, data, key, timeout);
        }

        public static string Broadcast(string server, string key, string msg, List<string> clients = null, int timeout = 0)
        {
            string service = "network";
            string action = "broadcast";

            string data = (clients != null && clients.Count > 0 ? String.Join(",", clients.ToArray()) : "") + "|" + msg;

            return Call(server, service, action, data, key, timeout);
        }

        public static string RandomCall(Dictionary<string, List<string>> remoteServers, string service, string action, string data)
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
                            result = RemoteCaller.Call(remoteUrl, service, action, data, svrKey);
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
