using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class NewtonJsonCodec : IJsonCodec
    {
        public string ToJsonString(object obj)
        {
            string str = "";
            try
            {
                str = JsonConvert.SerializeObject(obj);
            }
            catch { }
            return str;
        }

        public T ToJsonObject<T>(string str) where T : class
        {
            try
            {
                if (str.Length > 0) return JsonConvert.DeserializeObject<T>(str);
                else return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
