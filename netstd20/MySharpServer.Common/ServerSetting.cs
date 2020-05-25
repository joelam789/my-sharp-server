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

        public bool AllowParentPath { get; set; }

        public string CertFile { get; set; }

        public string CertKey { get; set; }

        public ServerSetting()
        {
            WorkIp = "0.0.0.0";
            WorkProtocol = "http";
            WorkPort = 9991;

            AccessUrl = "";

            AllowOrigin = "";

            AllowParentPath = false;

            CertFile = "";
            CertKey = "";
        }
    }

    public class CommonServerNodeSetting
    {
        public ServerSetting InternalServerSetting { get; set; }
        public ServerSetting PublicServerSetting { get; set; }

        public string NodeName { get; set; }
        public string GroupName { get; set; }

        public string Services { get; set; }

        public CommonServerNodeSetting()
        {
            InternalServerSetting = null;
            PublicServerSetting = null;
            
            NodeName = "";
            GroupName = "";

            Services = "";
        }
    }

    public class CommonServerContainerSetting
    {
        // may set it to empty string if want to run in standalone mode
        public string ServerInfoStorage { get; set; }

        public List<CommonServerNodeSetting> ServerNodeSettings { get; set; }

        public CommonServerContainerSetting()
        {
            ServerInfoStorage = "";
            ServerNodeSettings = new List<CommonServerNodeSetting>();
        }
    }
}
