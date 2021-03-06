﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MySharpServer.Common
{
    public interface IServerNode
    {
        void HandleRequest(RequestContext request);

        object EmitLocalEvent(string eventName, object eventData);

        ServiceCollection GetLocalServices();

        string GetInternalAccessUrl();
        string GetPublicAccessUrl();

        IWebServer GetInternalServer();
        IWebServer GetPublicServer();

        string GetName();
        string GetGroup();

        IServerLogger GetLogger();

        IJsonCodec GetJsonHelper();

        IDataAccessHelper GetDataHelper();

        Dictionary<string, List<string>> GetRemoteServices();

    }
}
