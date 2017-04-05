﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySharpServer.Common
{
    public interface IServerNode
    {
        void HandleRequest(RequestContext request);

        object EmitLocalEvent(string eventName, object eventData);

        ServiceCollection GetLocalServices();

        string GetInternalAccessUrl();

        string GetPublicAccessUrl();

        string GetName();

        string GetGroup();
    }
}