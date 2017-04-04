using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySharpServer.Common
{
    public interface IWebSession
    {
        string GetGroup();

        string GetProtocol();
        string GetRemoteAddress();

        void BeginResponse();
        void Send(string msg);
        void EndResponse();

        void CloseConnection();
    }
}
