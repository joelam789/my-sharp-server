using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class NewtonJsonCodec : IJsonCodec
    {
        private ExpandoObjectConverter m_MapConverter = new ExpandoObjectConverter();

        public string ToJsonString(object obj)
        {
            string str = "";
            try
            {
                if (obj != null) str = JsonConvert.SerializeObject(obj);
            }
            catch { }
            return str;
        }

        public object ToJsonObject(string str)
        {
            try
            {
                if (!string.IsNullOrEmpty(str))
                {
                    return JsonConvert.DeserializeObject(str);
                }
                else return null;
            }
            catch
            {
                return null;
            }
        }

        public IDictionary<string, object> ToDictionary(string str)
        {
            try
            {
                if (!string.IsNullOrEmpty(str))
                {
                    return JsonConvert.DeserializeObject<ExpandoObject>(str, m_MapConverter);
                }
                else return null;
            }
            catch
            {
                return null;
            }
        }

        public T ToJsonObject<T>(string str) where T : class
        {
            try
            {
                if (!string.IsNullOrEmpty(str))
                {
                    //if (typeof(T) == typeof(ExpandoObject)) return JsonConvert.DeserializeObject<ExpandoObject>(str, m_MapConverter) as T;
                    //else return JsonConvert.DeserializeObject<T>(str);
                    return JsonConvert.DeserializeObject<T>(str);
                }
                else return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
