using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySharpServer.Common
{
    public class ServiceCollection
    {
        public Dictionary<string, IActionCaller> PublicServices { get; private set; }
        public Dictionary<string, IActionCaller> InternalServices { get; private set; }

        public ServiceCollection(Dictionary<string, IActionCaller> publicServices, Dictionary<string, IActionCaller> internalServices)
        {
            PublicServices = publicServices;
            InternalServices = internalServices;
        }
    }
}
