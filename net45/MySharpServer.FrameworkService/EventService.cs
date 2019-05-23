using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MySharpServer.Common;

namespace MySharpServer.FrameworkService
{
    [Access(Name = "event", IsPublic = false)]
    public class EventService
    {
        [Access(Name = "on-connect")]
        public void OnConnect(string info)
        {
            Console.WriteLine("OnConnect: " + info);
        }

        [Access(Name = "on-disconnect")]
        public void OnDisconnect(string info)
        {
            Console.WriteLine("OnDisconnect: " + info);
        }
    }
}
