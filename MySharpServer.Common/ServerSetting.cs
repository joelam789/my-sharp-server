using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySharpServer.Common
{
    public class ServerSetting
    {
        public string ServerIp { get; set; }

        public int ServerPort { get; set; }

        public string ServerProtocol { get; set; }

        public string AllowOrigin { get; set; }

        public string CertFile { get; set; }

        public string CertKey { get; set; }

        public ServerSetting()
        {
            ServerIp = "0.0.0.0";
            ServerProtocol = "http";
            ServerPort = 9210;

            AllowOrigin = "";

            CertFile = "";
            CertKey = "";
        }
    }
}
