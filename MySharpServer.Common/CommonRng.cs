using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MySharpServer.Common
{
    public class CommonRng
    {
        private RNGCryptoServiceProvider m_rngsp = new RNGCryptoServiceProvider();

        // generate a random integer
        public int Next()
        {
            byte[] rb = { 0, 0, 0, 0 };
            m_rngsp.GetBytes(rb);
            int value = BitConverter.ToInt32(rb, 0);
            return value < 0 ? -value : value;
        }

        // generate a random integer, less than the maximum value
        public int Next(int max)
        {
            byte[] rb = { 0, 0, 0, 0 };
            m_rngsp.GetBytes(rb);
            int value = BitConverter.ToInt32(rb, 0) % max;
            return value < 0 ? -value : value;
        }

        // generate a random integer, between the minimum value and the maximum value
        public int Next(int min, int max)
        {
            return Next(max - min) + min;
        }
    }
}
