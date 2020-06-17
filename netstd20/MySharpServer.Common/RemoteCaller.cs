using System;
using System.Collections.Concurrent;
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
            JsonCodec = new SimpleJsonCodec();
            DefaultTimeout = 10 * 1000; // 10 seconds
            HttpConnectionLimit = Environment.ProcessorCount * 8;
        }

        public static IJsonCodec JsonCodec { get; set; }

        public static int DefaultTimeout { get; set; }

        public static int HttpConnectionLimit
        {
            get { return ServicePointManager.DefaultConnectionLimit; }
            set { ServicePointManager.DefaultConnectionLimit = value; }
        }

        public static async Task<object> Request(string url, object param, int timeout = 0)
        {
            return await Request(url, param, null, timeout);
        }

        public static async Task<T> Request<T>(string url, object param, int timeout = 0) where T : class
        {
            return await Request<T>(url, param, null, timeout);
        }

        public static async Task<object> Request(string url, object param, IDictionary<string, string> headers, int timeout = 0)
        {
            return JsonCodec.ToJsonObject(await Request<string>(url, param, headers, timeout));
        }

        public static async Task<T> Request<T>(string url, object param, IDictionary<string, string> headers, int timeout = 0) where T : class
        {
            T result = null;
            string input = null;

            if (param != null)
                input = param is string ? param.ToString() : JsonCodec.ToJsonString(param);

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);

            httpWebRequest.Accept = "*/*";
            httpWebRequest.UserAgent = "curl/7.50.0";
            httpWebRequest.ContentType = "text/plain";
            httpWebRequest.Method = param == null ? "GET" : "POST";

            if (headers != null)
            {
                var reqHeaders = new Dictionary<string, string>(headers);
                if (reqHeaders.ContainsKey("Content-Type"))
                {
                    httpWebRequest.ContentType = reqHeaders["Content-Type"];
                    reqHeaders.Remove("Content-Type");
                }
                foreach (var item in reqHeaders) httpWebRequest.Headers.Add(item.Key, item.Value);
            }

            httpWebRequest.Timeout = timeout > 0 ? timeout : DefaultTimeout;

            if (input != null)
            {
                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    await streamWriter.WriteAsync(input);
                    await streamWriter.FlushAsync();
                    streamWriter.Close();
                }
            }

            using (var response = await TryToGetResponse(httpWebRequest))
            {
                using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                {
                    string responseString = await streamReader.ReadToEndAsync();
                    var expectedType = typeof(T);
                    if (expectedType == typeof(string)) result = responseString as T;
                    else if (expectedType == typeof(IDictionary<string, object>)) result = JsonCodec.ToDictionary(responseString) as T;
                    else result = JsonCodec.ToJsonObject<T>(responseString);
                    streamReader.Close();
                }
            }

            return result;
        }

        public static async Task<object> CustomRequest(string url, object param, Action<HttpWebRequest> updateRequestParamFunc = null)
        {
            return JsonCodec.ToJsonObject(await CustomRequest<string>(url, param, updateRequestParamFunc));
        }

        public static async Task<T> CustomRequest<T>(string url, object param, Action<HttpWebRequest> updateRequestParamFunc = null) where T : class
        {
            T result = null;
            string input = null;

            if (param != null)
                input = param is string ? param.ToString() : JsonCodec.ToJsonString(param);

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);

            httpWebRequest.Accept = "*/*";
            httpWebRequest.UserAgent = "curl/7.50.0";
            httpWebRequest.ContentType = "text/plain";
            httpWebRequest.Method = param == null ? "GET" : "POST";
            httpWebRequest.Timeout = DefaultTimeout;

            updateRequestParamFunc?.Invoke(httpWebRequest);

            if (input != null)
            {
                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    await streamWriter.WriteAsync(input);
                    await streamWriter.FlushAsync();
                    streamWriter.Close();
                }
            }

            using (var response = await TryToGetResponse(httpWebRequest))
            {
                using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                {
                    string responseString = await streamReader.ReadToEndAsync();
                    var expectedType = typeof(T);
                    if (expectedType == typeof(string)) result = responseString as T;
                    else if (expectedType == typeof(IDictionary<string, object>)) result = JsonCodec.ToDictionary(responseString) as T;
                    else result = JsonCodec.ToJsonObject<T>(responseString);
                    streamReader.Close();
                }
            }

            return result;
        }

        public static async Task<string> Call(string url, string service, string action, string data, string key = "", int timeout = 0)
        {
            string result = "";

            string input = data == null ? "" : data;

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
                await streamWriter.WriteAsync(input);
                await streamWriter.FlushAsync();
                streamWriter.Close();
            }

            using (var response = await TryToGetResponse(httpWebRequest))
            {
                using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                {
                    result = await streamReader.ReadToEndAsync();
                    streamReader.Close();
                }
            }

            return result;
        }

        public static async Task<WebResponse> TryToGetResponse(HttpWebRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("HttpWebRequest");
            }

            WebResponse response = null;

            try
            {
                response = await request.GetResponseAsync();
            }
            catch (WebException ex)
            {
                response = ex.Response;
                if (response == null) throw;
            }

            return response;
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

        public static string RandomPickPublicServiceUrl(Dictionary<string, List<string>> remoteServices, string serviceName)
        {
            var publicUrl = "";
            List<string> serviceList = null;
            if (remoteServices != null && remoteServices.TryGetValue(serviceName, out serviceList))
            {
                if (serviceList != null && serviceList.Count > 0)
                {
                    var remoteInfoParts = RandomPicker.Pick<string>(serviceList).Split('|');
                    if (remoteInfoParts.Length >= 2) // name | url | key
                    {
                        var urls = remoteInfoParts[1].Split(','); // private(internal) url , public url
                        if (urls.Length >= 2) publicUrl = urls[1];
                    }
                }
            }
            return publicUrl;
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
                        string remoteUrl = remoteInfoParts[1].Split(',')[0]; // name | url | key
                        string svrKey = remoteInfoParts.Length >= 3 ? remoteInfoParts[2] : "";
                        return await Call(remoteUrl, service, action, data, svrKey, timeout);
                    }
                }
            }
            return "";
        }

        public static async Task<string> SpecifiedCall(Dictionary<string, List<string>> remoteServers, string server, string service, string action, string data, int timeout = 0)
        {
            List<string> remoteServerList = null;
            if (remoteServers != null && remoteServers.TryGetValue(service, out remoteServerList))
            {
                if (remoteServerList != null && remoteServerList.Count > 0)
                {
                    foreach (var remoteInfo in remoteServerList)
                    {
                        var remoteInfoParts = remoteInfo.Split('|');
                        if (remoteInfoParts.Length >= 2)
                        {
                            string remoteServer = remoteInfoParts[0]; // name | url | key
                            string remoteUrl = remoteInfoParts[1].Split(',')[0]; // name | url | key
                            string svrKey = remoteInfoParts.Length >= 3 ? remoteInfoParts[2] : "";

                            if (remoteServer.Length > 0 && remoteServer == server)
                            {
                                return await Call(remoteUrl, service, action, data, svrKey, timeout);
                            }
                        }
                    }
                }
            }
            return "";
        }

        public static async Task<Dictionary<string, string>> BroadcastCall(Dictionary<string, List<string>> remoteServers, string service, string action, string data, int timeout = 0)
        {
            List<Task> tasks = new List<Task>();
            Dictionary<string, string> results = null;
            var mappedOutput = new ConcurrentDictionary<string, string>();

            List<string> remoteServerList = null;
            if (remoteServers != null && remoteServers.TryGetValue(service, out remoteServerList))
            {
                if (remoteServerList != null && remoteServerList.Count > 0)
                {
                    foreach (var remoteInfo in remoteServerList)
                    {
                        var remoteInfoParts = remoteInfo.Split('|');
                        if (remoteInfoParts.Length >= 2)
                        {
                            string remoteUrl = remoteInfoParts[1].Split(',')[0]; // name | url | key
                            string svrKey = remoteInfoParts.Length >= 3 ? remoteInfoParts[2] : "";

                            tasks.Add(Task.Run(async () =>
                            {
                                try
                                {
                                    string result = await Call(remoteUrl, service, action, data, svrKey, timeout);
                                    mappedOutput.TryAdd(remoteInfo, result);
                                }
                                catch { }

                            }));
                        }
                    }

                    try
                    {
                        await Task.WhenAll(tasks.ToArray());
                    }
                    catch { }

                }
            }

            results = new Dictionary<string, string>(mappedOutput);

            return results;
        }

        // just an experimental function...
        public static async Task<string> MapReduceCall(Dictionary<string, List<string>> remoteServers, string service, string action, string data, 
            Func<string, List<string>, Dictionary<string, string>> mapFunc, Func<Dictionary<string, string>, string> reduceFunc, int timeout = 0)
        {
            List<string> remoteServerList = null;
            if (remoteServers != null && remoteServers.TryGetValue(service, out remoteServerList))
            {
                if (remoteServerList != null && remoteServerList.Count > 0)
                {
                    List<Task> tasks = new List<Task>();
                    Dictionary<string, string> mappedInput = mapFunc(data, remoteServerList);
                    var mappedOutput = new ConcurrentDictionary<string, string>();

                    //int avgTimeout = timeout / mappedInput.Count;
                    //if (avgTimeout < 0) avgTimeout = 0;
                    //if (avgTimeout > 0) avgTimeout += 10; // ...

                    foreach (var mappedItem in mappedInput)
                        mappedOutput.TryAdd(mappedItem.Key, null);

                    foreach (var remoteInfo in remoteServerList)
                    {
                        if (!mappedInput.ContainsKey(remoteInfo)) continue;

                        string currentData = mappedInput[remoteInfo];
                        mappedOutput[remoteInfo] = null;

                        var remoteInfoParts = remoteInfo.Split('|');
                        if (remoteInfoParts.Length >= 2)
                        {
                            string remoteUrl = remoteInfoParts[1].Split(',')[0]; // name | url | key
                            string svrKey = remoteInfoParts.Length >= 3 ? remoteInfoParts[2] : "";

                            tasks.Add(Task.Run(async () =>
                            {
                                try
                                {
                                    string reply = await Call(remoteUrl, service, action, currentData, svrKey, timeout);
                                    mappedOutput[remoteInfo] = reply;
                                }
                                catch { }

                            }));
                        }
                        
                    }

                    try
                    {
                        await Task.WhenAll(tasks.ToArray());
                    }
                    catch { }

                    var output = new Dictionary<string, string>(mappedOutput);

                    return reduceFunc(output);

                }
            }
            return "";
        }
    }
}
