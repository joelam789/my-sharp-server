using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySharpServer.Common
{
    public class ServerSetting
    {
        public string WorkIp { get; set; }

        public int WorkPort { get; set; }

        public string WorkProtocol { get; set; }

        public string AccessUrl { get; set; }

        public string AllowOrigin { get; set; }

        public string CertFile { get; set; }

        public string CertKey { get; set; }

        public ServerSetting()
        {
            WorkIp = "0.0.0.0";
            WorkProtocol = "http";
            WorkPort = 9991;

            AccessUrl = "http://127.0.0.1:9991";

            AllowOrigin = "";

            CertFile = "";
            CertKey = "";
        }
    }
}
