using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Newtonsoft.Json;

using MySharpServer.Common;
using MySharpServer.Framework;

namespace MySharpServerExample.ServerApp
{
    public partial class MainForm : Form
    {
        ServerNode m_ServerNode = null;

        ServerSetting m_InternalSetting = null;
        ServerSetting m_PublicSetting = null;

        string m_NodeName = "";
        string m_GroupName = "";

        string m_StorageName = "";

        List<string> m_ServiceFileNames = new List<string>();

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            CommonLog.SetGuiControl(this, mmLog);

            var appSettings = ConfigurationManager.AppSettings;

            var allKeys = appSettings.AllKeys;

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
        }

        private async void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (m_ServerNode != null) await m_ServerNode.Stop();
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            if (m_ServerNode != null && !m_ServerNode.IsWorking())
            {
                CommonLog.Info("Starting...");
                //m_ServerNode.Start(m_InternalSetting, m_PublicSetting);
                await m_ServerNode.StartStandaloneMode(m_PublicSetting);
                await Task.Delay(50);
                if (m_ServerNode.IsWorking())
                {
                    CommonLog.Info("Server Started");
                    if (!m_ServerNode.IsStandalone()) CommonLog.Info("Internal URL: " + m_ServerNode.GetInternalAccessUrl());
                    CommonLog.Info("Public URL: " + m_ServerNode.GetPublicAccessUrl());
                }
            }
        }

        private async void btnStop_Click(object sender, EventArgs e)
        {
            if (m_ServerNode != null && m_ServerNode.IsWorking()) await m_ServerNode.Stop();
        }
    }
}
