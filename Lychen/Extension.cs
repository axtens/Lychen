using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lychen
{
    public static class Extension
    {
        public static string GetTypeString(object thing) => thing == null ? "null" : thing.GetType().ToString();
        public static System.Type GetType(object thing) => thing.GetType();
    }
}
