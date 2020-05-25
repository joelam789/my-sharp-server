using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;

using NLog;
using NLog.Config;

using Newtonsoft.Json;

using MySharpServer.Common;
using MySharpServer.Framework;

namespace MySharpServerExample.ServerApp
{
    class Program
    {
        /*
        static ServerNode m_ServerNode = null;

        static ServerSetting m_InternalSetting = null;
        static ServerSetting m_PublicSetting = null;

        static string m_NodeName = "";
        static string m_GroupName = "";

        static string m_StorageName = "";

        static List<string> m_ServiceFileNames = new List<string>();
        */

        static CommonServerContainerSetting m_ServerSetting = null;
        static CommonServerContainer m_Server = null;

        static void Main(string[] args)
        {
            LogManager.Configuration = new XmlLoggingConfiguration($"{AppContext.BaseDirectory}/NLog.config");

            Console.WriteLine("Loading app.config...");

            var appSettings = ConfigurationManager.AppSettings;

            var allKeys = appSettings.AllKeys;

            /*
            foreach (var key in allKeys)
            {
                if (key == "InternalServer") m_InternalSetting = JsonConvert.DeserializeObject<ServerSetting>(appSettings[key]);
                if (key == "PublicServer") m_PublicSetting = JsonConvert.DeserializeObject<ServerSetting>(appSettings[key]);

                if (key == "NodeName") m_NodeName = appSettings[key];
                if (key == "GroupName") m_GroupName = appSettings[key];
                if (key == "ServerInfoStorageName") m_StorageName = appSettings[key];

                if (key == "Services")
                {
                    var fileNames = appSettings[key].Split(',');
                    m_ServiceFileNames.Clear();
                    m_ServiceFileNames.AddRange(fileNames);
                }
            }

            if (m_ServerNode == null)
            {
                m_ServerNode = new ServerNode(m_NodeName, m_GroupName, CommonLog.GetLogger());
                m_ServerNode.SetServerInfoStorage(m_StorageName);
                m_ServerNode.ResetLocalServiceFiles(m_ServiceFileNames);
            }
            */

            foreach (var key in allKeys)
            {
                if (key == "AppServerSetting")
                    m_ServerSetting = JsonConvert.DeserializeObject<CommonServerContainerSetting>(appSettings[key]);
            }

            if (m_Server == null && m_ServerSetting != null)
            {
                m_Server = new CommonServerContainer();
            }

            Console.WriteLine("Start server...");

            /*
            Task.Run(async () =>
            {
                if (m_ServerNode != null && !m_ServerNode.IsWorking())
                {
                    await m_ServerNode.Start(m_InternalSetting, m_PublicSetting);
                    //await m_ServerNode.StartStandaloneMode(m_PublicSetting);
                    await Task.Delay(50);
                    if (m_ServerNode.IsWorking())
                    {
                        CommonLog.Info("Server Started");
                        if (!m_ServerNode.IsStandalone()) CommonLog.Info("Internal URL: " + m_ServerNode.GetInternalAccessUrl());
                        CommonLog.Info("Public URL: " + m_ServerNode.GetPublicAccessUrl());
                    }
                }

                Console.WriteLine("Press any key to quit...");
                Console.ReadLine();

                Console.WriteLine("Stop server...");
                if (m_ServerNode != null) await m_ServerNode.Stop();

                Console.WriteLine("Done!");

            }).Wait();
            */

            if (m_Server != null && !m_Server.IsWorking() && m_ServerSetting != null)
            {
                Console.WriteLine("Starting...");
                m_Server.Start(m_ServerSetting, CommonLog.GetLogger());

                Console.WriteLine();
                Console.WriteLine("Press any key to quit...");
                Console.ReadLine();

                Console.WriteLine("Stop server...");
                if (m_Server != null) m_Server.Stop();

                Console.WriteLine();
                Console.WriteLine("Press any key again to end the process");
                Console.ReadLine();

                Console.WriteLine("- END -");
            }
        }
    }
}
