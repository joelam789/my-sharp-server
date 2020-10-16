using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NLog;
using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class ServerLogger: IServerLogger
    {
        protected static Logger m_Logger = null;

        public ServerLogger()
        {
            if (m_Logger == null) m_Logger = NLog.LogManager.GetCurrentClassLogger();
        }

        public virtual void Info(string msg)
        {
            if (m_Logger != null) m_Logger.Info(msg);
        }

        public virtual void Debug(string msg)
        {
            if (m_Logger != null) m_Logger.Debug(msg);
        }

        public virtual void Warn(string msg)
        {
            if (m_Logger != null) m_Logger.Warn(msg);
        }

        public virtual void Error(string msg)
        {
            if (m_Logger != null) m_Logger.Error(msg);
        }
    }
}
