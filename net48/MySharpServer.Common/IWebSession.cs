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

        string GetRequestPath();

        void BeginResponse();
        Task Send(string msg, IDictionary<string, string> metadata = null, int httpStatusCode = 0, string httpReasonPhrase = null);
        void EndResponse();

        bool IsConnected();
        void CloseConnection();
    }
}
