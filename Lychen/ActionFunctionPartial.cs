using System;
using System.IO;
using System.Reflection;
using Microsoft.ClearScript;
using NLog;

namespace Lychen
{
    internal class Status
    {
        public string Error { get; set; }
        public object Cargo { get; set; }
    }

    public static partial class Program
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private static void Attach2(string dllPath, string name = "")
        {
            var htc = new HostTypeCollection();
            try
            {
                //var assem = System.Reflection.Assembly.LoadFrom(dllPath);
                var assem = Assembly.Load(AssemblyName.GetAssemblyName(dllPath));
                htc.AddAssembly(assem);
                v8.AddHostObject(name, htc); //FIXME checkout the hosttypes
                Console.WriteLine($"Attached {dllPath} as {name}");
            }
            catch (ReflectionTypeLoadException rtle)
            {
                foreach (var item in rtle.LoaderExceptions)
                {
                    Console.WriteLine(item.Message);
                    logger.Error(item.Message);
                }
            }
            catch (FileNotFoundException fnfe)
            {
                Console.WriteLine(fnfe.Message);
                logger.Error(fnfe.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                logger.Error(e.Message);
            }
        }

        private static void Attach(string dllPath, string name = "")
        {
            var htc = new HostTypeCollection();
            try
            {
                //var assem = System.Reflection.Assembly.LoadFrom(dllPath);
                var assem = Assembly.Load(AssemblyName.GetAssemblyName(dllPath));
                htc.AddAssembly(assem);
                if (name.Length == 0) name = assem.FullName.Split(',')[0];
                v8.AddHostObject(name, htc);
                Console.WriteLine($"Attached {dllPath} as {name}");
            }
            catch (ReflectionTypeLoadException rtle)
            {
                foreach (var item in rtle.LoaderExceptions)
                {
                    Console.WriteLine(item.Message);
                    logger.Error(item.Message);
                }
            }
            catch (FileNotFoundException fnfe)
            {
                Console.WriteLine(fnfe.Message);
                logger.Error(fnfe.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                logger.Error(e.Message);
            }
        }

        private static void Attach(string obj)
        {
            Attach(obj, string.Empty);
        }

        private static string[] Glob(string wild)
        {
            var path = Path.GetDirectoryName(wild);
            if (path.Length == 0) path = ".\\";
            wild = Path.GetFileName(wild);
            return Directory.GetFiles(path, wild);
        }

        private static Status Include(string arg)
        {
            if (File.Exists(arg))
                try
                {
                    v8.Execute(File.ReadAllText(arg));
                    return new Status
                    {
                        Error = null,
                        Cargo = null
                    };
                }
                catch (ScriptEngineException see)
                {
                    return new Status
                    {
                        Cargo = null,
                        Error = see.Message
                    };
                }

            return new Status
            {
                Error = arg + " not found.",
                Cargo = null
            };
        }
    }
}