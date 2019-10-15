﻿using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Microsoft.ClearScript.Windows;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Lychen
{
    static partial class Program
    {
        private static List<string> GatherDependencies = new List<string>();

        private static void AddHostType(ref V8ScriptEngineFlags v8, string v, Type type)
        {
            if (Program.Settings.ContainsKey("/TRACE"))
            {
                logger.Error("AddHostType {0}", v);
            }
            Program.v8.AddHostType(v, type);
        }

        private static void AddHostObject(ref V8ScriptEngine v8, string v, object obj)
        {
            if (Program.Settings.ContainsKey("/TRACE"))
            {
                logger.Error("AddHostObject {0}", v);
            }
            Program.v8.AddHostObject(v, obj);
        }

        private static void AddHostSymbols(ref V8ScriptEngine v8, List<string> usualCollection)
        {
            var htc = new HostTypeCollection();

            foreach (var assembly in usualCollection)
            {
                if (Settings.ContainsKey("/TRACE"))
                {
                    logger.Error("AddHostAssembly {0}", assembly);
                }
                htc.AddAssembly(assembly);
            }

            AddHostObject(ref v8, "CS", htc);

            //var las = new List<Tuple<Assembly, int>>();

            if (Settings.ContainsKey("/ASSEMBLIES"))
            {
                var assemblies = Settings["/ASSEMBLIES"].ToString().Split(',');
                foreach (var assembly in assemblies)
                {
                    //Gather(assembly, ref las);
                    Attach(assembly);
                    logger.Info($"Attached {assembly}");
                }
            }

            //logger.Info(Newtonsoft.Json.JsonConvert.SerializeObject(las));

            //if (las.Count > 0)
            //   AssignHostObjectFromListOfAssemblies(las, "ASSEMBLIES");

        }

        private static void ProcessLocalAssemblyLists()
        {
            List<Tuple<Assembly, int>> las = new List<Tuple<Assembly, int>>();

            var assemblyLists = Directory.GetFiles(".", "*.assemblylist");
            if (assemblyLists.Length == 0)
                return;

            foreach (var assemblyList in assemblyLists)
            {
                //Console.Error.Write($"Loading assembly from {assemblyList} ... ");
                var symbol = Path.GetFileNameWithoutExtension(assemblyList);
                var assemblyLines = File.ReadAllLines(assemblyList);
                var packagesLocation = assemblyLines.First();
                Settings["$PACKAGES"] = packagesLocation;
                foreach (var assembly in assemblyLines.Skip(1))
                {
                    if (assembly.StartsWith("!"))
                    {
                        continue;
                    }

                    Gather(assembly, ref las);
                }

                logger.Info(Newtonsoft.Json.JsonConvert.SerializeObject(las));

                if (las.Count > 0)
                {
                    AssignHostObjectFromListOfAssemblies(las, symbol);
                    las.Clear();
                }
            }
        }

        private static void AssignHostObjectFromListOfAssemblies(List<Tuple<Assembly, int>> las, string name)
        {
            var asms = las.OrderBy(e => -e.Item2).Select(e => e.Item1).Distinct().ToArray();

            var htc = new HostTypeCollection();
            foreach (var asm in asms)
            {
                logger.Info($"Attempting to add {asm.FullName} to htc");

                try
                {
                    htc.AddAssembly(asm);
                }
                catch (ReflectionTypeLoadException rtle)
                {
                    foreach (var item in rtle.LoaderExceptions.Distinct())
                    {
                        logger.Error(item.Message);
                    }
                }
                catch (InvalidOperationException ioe)
                {
                    logger.Error(ioe.Message);
                    logger.Error(ioe.StackTrace);
                }
                catch (Exception e)
                {
                    logger.Error(e.Message);
                    logger.Error(e.StackTrace);
                }
            }
            AddHostObject(ref v8, name, htc);
            logger.Error($"{htc.Count()} symbol{(htc.Count() == 1 ? "" : "s")} added to {name}");

            Console.Error.WriteLine($"{htc.Count()} symbol{(htc.Count() == 1 ? "" : "s")} added to {name}");
        }

        private static bool ContainsAssemblyAtDepthAndEarlier(List<Tuple<Assembly, int>> ltai, Assembly asm, int depth)
        {
            for (var i = depth; i >= 0; i--)
            {
                var matcher = new Tuple<Assembly, int>(asm, i);
                if (ltai.Contains(matcher))
                {
                    return true;
                }
            }
            return false;
        }

        private static void Gather(string dll, ref List<Tuple<Assembly, int>> assemblies, int depth = 0)
        {
            var dllInPackages = InPackages(dll);

            if (File.Exists(dllInPackages))
            {
                if (GatherDependencies.Contains(dllInPackages))
                {
                    logger.Warn("Ignored", dllInPackages);
                    return;
                }

                var assem = Assembly.Load(AssemblyName.GetAssemblyName(dllInPackages));

                if (!ContainsAssemblyAtDepthAndEarlier(assemblies, assem, depth))
                    assemblies.Add(new Tuple<Assembly, int>(assem, depth));

                var dependencies = GetDependentDlls(assem);

                if (dependencies.Item1.Length > 0)
                {
                    foreach (var dependency in dependencies.Item2)
                    {
                        Gather(dependency, ref assemblies, depth + 1);
                        GatherDependencies.Add(dependency);
                    }
                }


            }
            else
            {
                logger.Error($"{dllInPackages} doesn't exist");

            }
        }


        private static string InPackages(string dependency)
        {
            var packages = Settings["$PACKAGES"].ToString();
            if (Directory.Exists(packages))
            {
                var dir = Directory.GetFiles(packages, Path.GetFileName(dependency), SearchOption.AllDirectories);
                foreach (var dll in dir)
                {
                    if (dll.ToUpperInvariant().Contains("LIB\\NET48")) return dll;
                    if (dll.ToUpperInvariant().Contains("LIB\\NET47")) return dll;
                    if (dll.ToUpperInvariant().Contains("LIB\\NET46")) return dll;
                    if (dll.ToUpperInvariant().Contains("LIB\\NET45")) return dll;
                    if (dll.ToUpperInvariant().Contains("LIB\\NETSTANDARD2.1")) return dll;
                    if (dll.ToUpperInvariant().Contains("LIB\\NETSTANDARD2.0")) return dll;
                }
            }
            return null;
        }

        private static Tuple<string[], string[]> GetDependentDlls(Assembly assembly)
        {
            AssemblyName[] asm = assembly.GetReferencedAssemblies();
            var packagespaths = new List<string>();
            var virtualpaths = new List<string>();

            for (int t = asm.Length - 1; t >= 0; t--)
            {
                string path = Path.GetFullPath(asm[t].Name + ".dll");

                var newpath = InPackages(path);
                if (newpath == null)
                {
                    virtualpaths.Add(path);
                }
                else
                {
                    packagespaths.Add(newpath);
                }
            }

            return new Tuple<string[], string[]>(packagespaths.ToArray(), virtualpaths.ToArray());
        }
    }
}
