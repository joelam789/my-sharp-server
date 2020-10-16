using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySharpServer.Common
{
    public interface IServerLogger
    {
        void Info(string msg);

        void Debug(string msg);

        void Warn(string msg);

        void Error(string msg);
    }
}
