using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MySharpServer.Common
{
    public class SimpleJsonCodec: IJsonCodec
    {
        public string ToJsonString(object obj)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(obj.GetType());
            using (MemoryStream ms = new MemoryStream())
            {
                serializer.WriteObject(ms, obj);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public object ToJsonObject(string str)
        {
            // should not work... may use JavaScriptSerializer, but need System.Web
            return ToJsonObject<ExpandoObject>(str);
        }

        public T ToJsonObject<T>(string str) where T : class
        {
            if (str == null || str.Length <= 0) return null;
            else
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(str)))
                {
                    return serializer.ReadObject(ms) as T;
                }
            }
        }

    }
}
