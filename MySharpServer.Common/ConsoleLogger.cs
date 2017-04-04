using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySharpServer.Common
{
    public class ConsoleLogger : IServerLogger
    {
        public void Info(string msg)
        {
            Console.WriteLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "][INFO] " + msg);
        }

        public void Debug(string msg)
        {
            Console.WriteLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "][DEBUG] " + msg);
        }

        public void Warn(string msg)
        {
            Console.WriteLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "][WARN] " + msg);
        }

        public void Error(string msg)
        {
            Console.WriteLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "][ERROR] " + msg);
        }
    }
}
