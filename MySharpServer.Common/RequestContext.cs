using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySharpServer.Common
{
    public class RequestContext
    {
        public static readonly int FLAG_PUBLIC = 1;

        public IWebSession Session { get; private set; }

        public IDataHelper Helper { get; set; }

        public IJsonCodec JsonCodec { get; set; }

        public IServerLogger LocalLogger { get; set; }
        public ServiceCollection LocalServices { get; set; }
        public Dictionary<string, List<string>> RemoteServices { get; set; }

        public List<String> PathParts { get; private set; } // request path
        public Object Data { get; private set; } // request data, and here we support only text data for now

        public Int32 Flags { get; private set; }

        public String Key { get; private set; }
        public String LocalServer { get; set; }
        public String EntryServer { get; set; }
        public String ClientAddress { get; set; }

        public RequestContext()
        {
            Session = null;
            Helper = null;

            JsonCodec = null;

            LocalLogger = null;
            LocalServices = null;
            RemoteServices = null;

            PathParts = null;
            Data = null;

            Flags = 0;

            Key = null;
            LocalServer = null;
            EntryServer = null;
            ClientAddress = null;
        }

        public RequestContext(String content, Int32 flags): this()
        {
            Flags = flags;
            if (content != null)
            {
                Key = "";
                Data = "";
                LocalServer = "";
                EntryServer = "";
                ClientAddress = "";
                PathParts = new List<string>();
                bool isFromPublic = (flags & FLAG_PUBLIC) != 0;
                int pos = content.IndexOf("/{");
                if (pos >= 0)
                {
                    Data = content.Substring(pos + 1);
                    var parts = content.Substring(0, pos).Split('/');
                    for (int i = 0; i < parts.Length; i++)
                    {
                        var part = parts[i];

                        if (part.Length > 0) PathParts.Add(part);
                        else if (i == parts.Length - 1) PathParts.Add("");
                        else continue;

                        if (PathParts.Count >= 2 && isFromPublic) break;
                        if (PathParts.Count >= 4) break;
                    }

                    if (!isFromPublic)
                    {
                        if (PathParts.Count == 3)
                        {
                            Key = PathParts[2];
                            PathParts.RemoveAt(2);
                        }
                        else if (PathParts.Count == 4)
                        {
                            EntryServer = PathParts[2];
                            ClientAddress = PathParts[3];
                            PathParts.RemoveRange(2, 2);
                        }
                    }

                }
                else
                {
                    var parts = content.Split('/');
                    for (int i = 0; i < parts.Length; i++)
                    {
                        var part = parts[i];
                        if (part.Length > 0) PathParts.Add(part);
                        else if (i == parts.Length - 1) PathParts.Add("");
                        else continue;
                    }

                    if (PathParts.Count >= 3)
                    {
                        Data = PathParts.Last();
                        PathParts.RemoveAt(PathParts.Count - 1);
                    }

                    if (isFromPublic)
                    {
                        if (PathParts.Count > 2)
                        {
                            PathParts.RemoveRange(2, PathParts.Count - 2);
                        }
                    }
                    else
                    {
                        if (PathParts.Count == 3)
                        {
                            Key = PathParts[2];
                            PathParts.RemoveAt(2);
                        }
                        else if (PathParts.Count >= 4)
                        {
                            EntryServer = PathParts[2];
                            ClientAddress = PathParts[3];
                            PathParts.RemoveRange(2, PathParts.Count - 2);
                        }
                    }
                }
            }
        }

        public RequestContext(IWebSession session, String content, Int32 flags)
            : this(content, flags)
        {
            Session = session;
        }

        public RequestContext(ServiceCollection localServices, String content, Int32 flags)
            : this(content, flags)
        {
            LocalServices = localServices;
        }

        public RequestContext(Dictionary<string, List<string>> remoteServices, String content, Int32 flags)
            : this(content, flags)
        {
            RemoteServices = remoteServices;
        }

        public RequestContext(IWebSession session, ServiceCollection localServices, String content, Int32 flags)
            : this(content, flags)
        {
            Session = session;
            LocalServices = localServices;
        }

        public RequestContext(IWebSession session, Dictionary<string, List<string>> remoteServices, String content, Int32 flags)
            : this(content, flags)
        {
            Session = session;
            RemoteServices = remoteServices;
        }

        public RequestContext(IWebSession session, ServiceCollection localServices, Dictionary<string, List<string>> remoteServices, String content, Int32 flags)
            : this(content, flags)
        {
            Session = session;
            LocalServices = localServices;
            RemoteServices = remoteServices;
        }

        public RequestContext(IDataHelper helper, Object data)
            : this()
        {
            Helper = helper;
            Data = data;
        }

        public RequestContext(IDataHelper helper, ServiceCollection localServices, Object data)
            : this()
        {
            Helper = helper;
            Data = data;
            LocalServices = localServices;
        }

        public RequestContext(IDataHelper helper, Dictionary<string, List<string>> remoteServices, Object data)
            : this()
        {
            Helper = helper;
            Data = data;
            RemoteServices = remoteServices;
        }

        public RequestContext(IDataHelper helper, ServiceCollection localServices, Dictionary<string, List<string>> remoteServices, Object data)
            : this()
        {
            Helper = helper;
            Data = data;
            LocalServices = localServices;
            RemoteServices = remoteServices;
        }

        public bool IsFromPublic()
        {
            return (Flags & FLAG_PUBLIC) != 0;
        }
    }
}
