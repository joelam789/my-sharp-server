using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MySharpServer.Common
{
    public interface IWebSession
    {
        string GetGroup();

        string GetProtocol();
        string GetRemoteAddress();

        void BeginResponse();
        Task Send(string msg);
        void EndResponse();

        void CloseConnection();
    }
}
