using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class ServiceRequestContext
    {
        public ServiceWrapper Service { get; private set; }
        public RequestContext Context { get; private set; }

        public bool IsPublicRequest { get; private set; }

        public ServiceRequestContext(ServiceWrapper service, RequestContext context, bool isPublic)
        {
            Service = service;
            Context = context;
            IsPublicRequest = isPublic;
        }
        
    }
}
