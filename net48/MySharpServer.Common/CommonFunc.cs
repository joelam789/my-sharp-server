using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MySharpServer.Common
{
    public static class CommonFunc
    {
        public static string GetMd5Hash(string input)
        {
            using (MD5 md5Hash = MD5.Create())
            {
                // Convert the input string to a byte array and compute the hash.
                byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

                // Create a new Stringbuilder to collect the bytes
                // and create a string.
                StringBuilder builder = new StringBuilder();

                // Loop through each byte of the hashed data 
                // and format each one as a hexadecimal string.
                for (int i = 0; i < data.Length; i++)
                {
                    builder.Append(data[i].ToString("x2"));
                }

                // Return the hexadecimal string.
                return builder.ToString();
            }
        }

        public static string GetLocalFileFullPath(string filepath)
        {
            var output = String.IsNullOrEmpty(filepath) ? "" : filepath.Trim();
            if (output.Length > 0)
            {
                output = output.Replace('\\', '/');
                if (output[0] != '/' && output.IndexOf(":/") != 1) // if it is not abs path
                {
                    string folder = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
                    if (folder == null || folder.Trim().Length <= 0)
                    {
                        var entry = Assembly.GetEntryAssembly();
                        var location = "";
                        try
                        {
                            if (entry != null) location = entry.Location;
                        }
                        catch { }
                        if (location != null && location.Length > 0)
                        {
                            folder = Path.GetDirectoryName(location);
                        }
                    }
                    if (folder != null && folder.Length > 0) output = folder.Replace('\\', '/') + "/" + output;
                }
            }
            return output;
        }
    }
}
