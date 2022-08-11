using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Lychen
{
    static partial class Program
    {
        public static V8ScriptEngine v8;
        public static Dictionary<string, object> Settings = new Dictionary<string, object>();

        public static string logFile;
        // behaviour change: if no file, run repl.

        [STAThread]
        private static int Main(string[] args)
        {
            logFile = SetupLogging();
            Settings["$EXEPATH"] = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Settings["$CURPATH"] = Directory.GetCurrentDirectory();

            LoadSettingsDictionary(args);

            var v8ScriptEngineFlags = V8ScriptEngineFlags.EnableDebugging
                                      | V8ScriptEngineFlags.EnableRemoteDebugging
                                      | V8ScriptEngineFlags.DisableGlobalMembers;

            if (Settings.ContainsKey("/DEBUG"))
            {
                v8ScriptEngineFlags |= V8ScriptEngineFlags.AwaitDebuggerAndPauseOnStart;
                if (Settings["/DEBUG"].GetType() != typeof(bool))
                {
                    var psi = new ProcessStartInfo(
                        $"{Settings["/DEBUG"]}.exe",
                        $"{Settings["/DEBUG"]}\"://inspect/#devices\"")
                    {
                        CreateNoWindow = true
                    };
                    Process.Start(psi);
                }
            }

            if (Settings.ContainsKey("/EXEDEBUG") && Settings["/EXEDEBUG"].GetType() == typeof(bool)) Debugger.Launch();

            v8 = new V8ScriptEngine(v8ScriptEngineFlags, 9229);

            AddSymbols();

            var script = string.Empty;
            var replLogFile = Path.GetTempFileName();

            var hasReplLog = Settings.ContainsKey("/LOG");
            if (hasReplLog)
                if (Settings["/LOG"].GetType() != typeof(bool))
                    replLogFile = Settings["/LOG"].ToString();

            ConnectoToScriptINI(script);

            if ((int)Settings["$ARGC"] > 0)
            {
                script = Settings["$ARG0"].ToString();
                if (!File.Exists(script))
                {
                    Console.WriteLine($"Script {script} not found.");
                    return 2;
                }

                ExecuteScript(script);
            }
            else
            {
                RunREPL(replLogFile);
            }

            Console.Error.WriteLine("NLog output in {0}", logFile);
            return 0;
        }

        private static string SetupLogging()
        {
            // Step 1. Create configuration object 
            var config = new LoggingConfiguration();

            // Step 2. Create targets and add them to the configuration 
            //var consoleTarget = new ColoredConsoleTarget();
            //config.AddTarget("console", consoleTarget);

            var fileTarget = new FileTarget();
            config.AddTarget("file", fileTarget);

            // Step 3. Set target properties 
            //consoleTarget.Layout = @"${date:format=HH\:mm\:ss} ${logger} ${message}";
            var logFile = Path.GetTempFileName();
            fileTarget.FileName = logFile;
            fileTarget.Layout = @"${date:format=HH\:mm\:ss} ${logger} ${message}";
            // Step 4. Define rules
            //var rule1 = new LoggingRule("*", LogLevel.Debug, consoleTarget);
            //config.LoggingRules.Add(rule1);

            var rule2 = new LoggingRule("*", LogLevel.Debug, fileTarget);
            config.LoggingRules.Add(rule2);

            // Step 5. Activate the configuration
            LogManager.Configuration = config;
            return logFile;
        }

        private static void ExecuteScript(string script)
        {
            object evaluand = null;

            if (script != string.Empty)
            {
                var context = v8.Compile(File.ReadAllText(script));
                try
                {
                    evaluand = v8.Evaluate(context);
                }
                catch (ScriptEngineException see)
                {
                    evaluand = "";
                    Console.WriteLine(see.ErrorDetails);
                    Console.WriteLine(see.StackTrace);
                }
                catch (NullReferenceException nre)
                {
                    evaluand = "";
                    Console.WriteLine(nre.Message);
                }
                catch (Exception e)
                {
                    evaluand = "";
                    Console.WriteLine(e.Message);
                }

                if (evaluand.GetType() != typeof(VoidResult)) Console.WriteLine($"{evaluand}");
            }
        }

        private static void RunREPL(string fileName)
        {
            string cmd;
            do
            {
                Console.Write(Settings["$PROMPT"]);
                cmd = Console.ReadLine();
                if (cmd == "bye") break;
                if (fileName != string.Empty) File.AppendAllText(fileName, cmd + "\r\n");

                object evaluand;
                try
                {
                    evaluand = v8.Evaluate(cmd);
                }
                catch (ScriptEngineException see)
                {
                    evaluand = "";
                    Console.WriteLine(see.ErrorDetails);
                    Console.WriteLine(see.StackTrace);
                }
                catch (NullReferenceException nre)
                {
                    evaluand = "";
                    Console.WriteLine(nre.Message);
                }
                catch (Exception e)
                {
                    evaluand = "";
                    Console.WriteLine(e.Message);
                }

                if (evaluand == null) evaluand = "(null)";

                if (evaluand.GetType() != typeof(VoidResult))
                {
                    Console.WriteLine($"{evaluand}");
                    if (fileName != string.Empty) File.AppendAllText(fileName, $"// {evaluand}\r\n");
                }
            } while (cmd != "bye");
        }

        private static void ConnectoToScriptINI(string script)
        {
            var iniName = Path.ChangeExtension(script, ".INI");
            var iniPath = Path.Combine(Settings["$EXEPATH"].ToString(), iniName);
            if (File.Exists(iniPath))
            {
                v8.AddHostObject("CSScriptINI", new INI(iniPath));
            }
            else
            {
                iniPath = Path.Combine(Settings["$CURPATH"].ToString(), iniName);
                v8.AddHostObject("CSScriptINI", new INI(iniPath));
            }
        }

        private static void LoadSettingsDictionary(string[] args)
        {
            var argv = new List<string>();
            var cnt = 0;
            var slashCnt = 0;
            foreach (var arg in args)
                if (arg.StartsWith("/"))
                {
                    slashCnt++;
                    if (arg.Contains(":") || arg.Contains("="))
                    {
                        var lhs = arg.Split(new[] { ':', '=' }, 2);
                        Settings[lhs[0]] = lhs[1];
                    }
                    else
                    {
                        Settings[arg] = true;
                    }
                }
                else
                {
                    Settings[$"$ARG{cnt}"] = arg;
                    cnt++;
                    argv.Add(arg);
                }

            Settings["$ARGC"] = cnt;
            Settings["$ARGV"] = argv.ToArray<string>();
            Settings["/COUNT"] = slashCnt;
            Settings["$PROMPT"] = "Lychen> ";
        }

        private static void AddSymbols()
        {
            AddInternalSymbols(ref v8);
            AddHostSymbols(ref v8);
            AddSystemSymbols(ref v8);
            ProcessLocalAssemblyLists();
            ProcessLocalAssemblyJsons();
        }

        private static void AddInternalSymbols(ref V8ScriptEngine v8)
        {
            v8.AddHostObject("V8", v8);
            v8.AddHostType("CSINI", typeof(INI));
            v8.AddHostType("CSKeyGenerator", typeof(KeyGenerator));
            v8.AddHostObject("CSSettings", Settings);
            v8.AddHostType("CSLychen", typeof(Program)); // Experimental. No idea if useful or dangerous.
            v8.AddHostType("CSReflection", typeof(Reflections));
            v8.AddHostType("CSExtension", typeof(Extension));
        }

        private static void AddSystemSymbols(ref V8ScriptEngine v8)
        {
            v8.AddHostType("CSFile", typeof(File));
            v8.AddHostType("CSConsole", typeof(Console));
            v8.AddHostType("CSPath", typeof(Path));
            v8.AddHostType("CSDirectory", typeof(Directory));
            v8.AddHostType("CSDirectoryInfo", typeof(DirectoryInfo));
            v8.AddHostType("CSEnvironment", typeof(Environment));
            v8.AddHostType("CSString", typeof(string));
            v8.AddHostType("CSDateTime", typeof(DateTime));
            v8.AddHostType("CSDebugger", typeof(Debugger));
        }

        private static void AddHostSymbols(ref V8ScriptEngine v8)
        {
            v8.Script.print = (Action<object>)Console.WriteLine;
            v8.Script.exit = (Action<int>)Environment.Exit;
            v8.Script.attach = (Action<string, string>)Attach;
            v8.Script.attach2 = (Action<string, string>)Attach2;
            v8.Script.glob = (Func<string, string[]>)Glob;
            v8.Script.include = (Func<string, Status>)Include;
            v8.Script.readline = (Func<string>)Console.ReadLine;

            v8.AddHostObject("CSExtendedHost", new ExtendedHostFunctions());
            v8.AddHostObject("CSHost", new HostFunctions());
            var htc = new HostTypeCollection();
            foreach (var assembly in new[]
                     {
                         "mscorlib", "System", "System.Core", "System.Data", "RestSharp", "WebDriver",
                         "WebDriver.Support"
                     }) htc.AddAssembly(assembly);

            if (Settings.ContainsKey("/ASSEMBLIES"))
            {
                var assemblies = Settings["/ASSEMBLIES"].ToString().Split(',');
                foreach (var assembly in assemblies)
                {
                    Assembly assem;
                    try
                    {
                        //assem = System.Reflection.Assembly.LoadFrom(assembly);
                        assem = Assembly.Load(AssemblyName.GetAssemblyName(assembly));
                        htc.AddAssembly(assem);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }

            //var globalSettingsFile = Path.Combine(Settings["$EXEPATH"].ToString(), "Lychen.assemblies");
            //if (File.Exists(globalSettingsFile))
            //{
            //    foreach (var assembly in File.ReadAllLines(globalSettingsFile))
            //    {
            //        System.Reflection.Assembly assem;
            //        try
            //        {
            //            //assem = System.Reflection.Assembly.LoadFrom(assembly);
            //            assem = Assembly.Load(AssemblyName.GetAssemblyName(assembly));
            //            htc.AddAssembly(assem);
            //        }
            //        catch (Exception e)
            //        {
            //            Console.WriteLine("{0}: {1}", assembly, e.Message);
            //        }
            //    }
            //}
            //else
            //{
            var localSettingsFile = Path.Combine(Settings["$CURPATH"].ToString(), "Lychen.assemblies");
            if (File.Exists(localSettingsFile))
                foreach (var assembly in File.ReadAllLines(localSettingsFile))
                {
                    Assembly assem;
                    try
                    {
                        //assem = System.Reflection.Assembly.LoadFrom(assembly);
                        assem = Assembly.Load(AssemblyName.GetAssemblyName(assembly));
                        htc.AddAssembly(assem);
                    }
                    catch (ReflectionTypeLoadException rtle)
                    {
                        foreach (var item in rtle.LoaderExceptions.Distinct())
                        {
                            logger.Error(item.Message);
                            Console.WriteLine(item.Message);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("{0}: {1}", assembly, e.Message);
                    }
                }

            //}
            v8.AddHostObject("CS", htc);
        }
    }
}