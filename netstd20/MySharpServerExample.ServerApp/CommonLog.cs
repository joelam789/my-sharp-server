using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MySharpServer.Common;
using MySharpServer.Framework;

namespace MySharpServerExample.ServerApp
{
    public static class CommonLog
    {
        static readonly ServerFormLogger m_Logger = null;

        static CommonLog()
        {
            m_Logger = new ServerFormLogger();
        }

        public static IServerLogger GetLogger()
        {
            return m_Logger;
        }

        public static void Info(string msg)
        {
            if (m_Logger != null) m_Logger.Info(msg);
        }

        public static void Debug(string msg)
        {
            if (m_Logger != null) m_Logger.Debug(msg);
        }

        public static void Warn(string msg)
        {
            if (m_Logger != null) m_Logger.Warn(msg);
        }

        public static void Error(string msg)
        {
            if (m_Logger != null) m_Logger.Error(msg);
        }
    }

    public class ServerFormLogger : ServerLogger
    {

        private void TryToSendLogToConsole(string msg)
        {
            //try
            //{
            //    Console.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss") + "]" + msg);
            //}
            //catch { }
        }

        public override void Info(string msg)
        {
            base.Info(msg);
            TryToSendLogToConsole("[INFO] " + msg);
        }

        public override void Debug(string msg)
        {
            base.Debug(msg);
            TryToSendLogToConsole("[DEBUG] " + msg);
        }

        public override void Warn(string msg)
        {
            base.Warn(msg);
            TryToSendLogToConsole("[WARN] " + msg);
        }

        public override void Error(string msg)
        {
            base.Error(msg);
            TryToSendLogToConsole("[ERROR] " + msg);
        }
    }
}
