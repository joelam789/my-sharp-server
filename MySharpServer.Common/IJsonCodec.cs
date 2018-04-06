using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySharpServer.Common
{
    public interface IJsonCodec
    {
        string ToJsonString(object obj);
        object ToJsonObject(string str);
        T ToJsonObject<T>(string str) where T : class;
    }
}
