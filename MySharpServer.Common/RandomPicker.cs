using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySharpServer.Common
{
    public static class RandomPicker
    {
        static readonly CommonRng m_Rng = null;

        static RandomPicker()
        {
            m_Rng = new CommonRng();
        }

        public static T Pick<T>(List<T> list)
        {
            if (list.Count == 1) return list[0];
            return list[m_Rng.Next() % list.Count];
        }
    }
}
