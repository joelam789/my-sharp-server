using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MySharpServer.Common
{
    public interface IActionCaller
    {
        Task<object> Call(string actionName, object param, bool publicOnly = true);
    }
}
