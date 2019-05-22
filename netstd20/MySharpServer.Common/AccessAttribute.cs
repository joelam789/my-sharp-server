using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySharpServer.Common
{
    public class AccessAttribute : Attribute
    {
        public string Name { get; set; }

        public bool IsPublic { get; set; }

        public string Version { get; set; } // not support for now

        public AccessAttribute()
        {
            Name = "";
            IsPublic = true;
            Version = "1.0.0";
        }
    }
}
