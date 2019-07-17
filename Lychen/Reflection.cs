using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.Remoting;

namespace Lychen
{
    public static class Reflection
    {
        public static Assembly GetAssemblyByName(string symbol)
        {
            if (Program.Settings.ContainsKey("/" + MethodBase.GetCurrentMethod().Name))
            {
                Debugger.Launch();
            }
            return Assembly.Load(symbol);
        }
        public static Assembly GetAssemblyByPath(string path)
        {
            if (Program.Settings.ContainsKey("/" + MethodBase.GetCurrentMethod().Name))
            {
                Debugger.Launch();
            }
            return Assembly.LoadFrom(path);
        }

        public static object InvokeInstance(string dll, string namespace_class, string method_name, params object[] arguments)
        {
            if (Program.Settings.ContainsKey("/" + MethodBase.GetCurrentMethod().Name))
            {
                Debugger.Launch();
            }
            var handle = Activator.CreateInstanceFrom(dll, namespace_class);
            Object p = handle.Unwrap();
            Type t = p.GetType();
            MethodInfo method = t.GetMethod(method_name);
            Object retVal = method.Invoke(p, arguments);
            return retVal;
        }

        public static object InvokeStatic(string pathToDLL, string namespaceClass, string methodName, params object[] arguments)
        {
            if (Program.Settings.ContainsKey("/" + MethodBase.GetCurrentMethod().Name))
            {
                Debugger.Launch();
            }

            object retVal = null;
            var assembly = System.Reflection.Assembly.LoadFrom(pathToDLL);
            foreach (var type in assembly.GetTypes())
            {
                if ((type.FullName == namespaceClass) && type.IsClass)
                {
                    var method = type.GetMethod(methodName, (BindingFlags.Static | BindingFlags.Public));
                    retVal = method.Invoke(null, arguments);
                    break;
                }
            }
            return retVal;
        }
    }
}
