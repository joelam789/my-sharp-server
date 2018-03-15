using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySharpServer.Common
{
    public interface IActionCaller
    {
        object Call(string actionName, object param, bool publicOnly = true);
    }
}
