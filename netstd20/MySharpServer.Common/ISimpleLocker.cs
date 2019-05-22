using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySharpServer.Common
{
    public interface ISimpleLocker: IDisposable
    {
        bool IsLocked { get; }
        string ErrorMessage { get; }
    }
}
