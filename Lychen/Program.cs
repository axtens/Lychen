using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using System.Data;

namespace Lychen
{
    static class Program
    {
        public static V8ScriptEngine v8 = null;
        public static Dictionary<string, object> Settings = new Dictionary<string, object>();

        static int Main(string[] args)
        {
            ParseArgsToSettings(args);

            var V8Setup = V8ScriptEngineFlags.EnableDebugging |
                V8ScriptEngineFlags.EnableRemoteDebugging |
                V8ScriptEngineFlags.DisableGlobalMembers;

            if (Settings.ContainsKey("/V8DEBUG"))
            {
                V8Setup |= V8ScriptEngineFlags.AwaitDebuggerAndPauseOnStart;
            }

            if (Settings.ContainsKey("/DEBUG") && Settings["/DEBUG"].GetType() == typeof(bool))
            {
                System.Diagnostics.Debugger.Launch();
            }

            v8 = new V8ScriptEngine(V8Setup, 9229);

            LoadGlobalFunctions();

            string script = string.Empty;
            string replFile = string.Empty;

            var hasRepl = Settings.ContainsKey("/REPL");
            if (hasRepl)
            {
                if (Settings["/REPL"].GetType() != typeof(bool))
                {
                    replFile = Settings["/REPL"].ToString();
                }
            }

            if ((int)Settings["$ARGC"] > 0)
            {
                script = Settings["$ARG1"].ToString();
                if (!File.Exists(script))
                {
                    Console.WriteLine($"Script {script} not found.");
                    return 2;
                }
                ConnectToScriptLIN(script);
            }
            else
            {
                if (!hasRepl)
                {
                    Console.WriteLine("No script.");
                    return 1;
                }
                ConnectToScriptLIN("repl.lin"); // FIXME. Put this somewhere useful
            }

            SetupIncludeFunction();

            object evaluand = null;

            if (script != string.Empty)
            {
                var context = v8.Compile(File.ReadAllText(script));
                evaluand = v8.Evaluate(context);
                if (evaluand.GetType() != typeof(Microsoft.ClearScript.VoidResult))
                {
                    Console.WriteLine($"{evaluand}");
                }
            }

            if (hasRepl)
            {
                RunREPL(replFile);
            }

            return 0;
        }

        private static void RunREPL(string fileName)
        {
            object evaluand = null;
            var cmd = string.Empty;
            do
            {
                Console.Write(">");
                cmd = Console.ReadLine();
                if (cmd == "bye")
                {
                    break;
                }
                if (fileName != string.Empty)
                {
                    File.AppendAllText(fileName, cmd + "\r\n");
                }

                try
                {
                    evaluand = v8.Evaluate(cmd);
                }
                catch (ScriptEngineException see)
                {
                    evaluand = "";
                    Console.WriteLine(see.Message);
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
                if (evaluand.GetType() != typeof(Microsoft.ClearScript.VoidResult))
                {
                    Console.WriteLine($"{evaluand}");
                    if (fileName != string.Empty)
                    {
                        File.AppendAllText(fileName, $"// {evaluand}\r\n");
                    }
                }
            } while (cmd != "bye");
        }

        private static void SetupIncludeFunction()
        {
            var obfusc = "CS" + KeyGenerator.GetUniqueKey(36);

            v8.AddHostObject(obfusc, v8);
            var includeCode = @"
            function include(fn) {
                if (CSFile.Exists(fn)) {
                    $SYM$.Evaluate(CSFile.ReadAllText(fn));
                } else {
                    throw 'File not found.';
                }
            }".Replace("$SYM$", obfusc);

            v8.Evaluate(includeCode);
        }

        private static void ConnectToScriptLIN(string script)
        {
            var ini = new INI(Path.ChangeExtension(script, ".LIN"));
            v8.AddHostObject("CSScriptINI", ini);
        }

        private static void ParseArgsToSettings(string[] args)
        {
            var argv = new List<string>();
            var cnt = 0;
            foreach (var arg in args)
            {
                if (arg.StartsWith("/"))
                {
                    if (arg.Contains(":") || arg.Contains("="))
                    {
                        var lhs = arg.Split(new char[] { ':', '=' }, 2);
                        Settings[lhs[0]] = lhs[1];
                    }
                    else
                    {
                        Settings[arg] = true;
                    }
                }
                else
                {
                    Settings[$"$ARG{cnt + 1}"] = arg;
                    cnt += 1;
                    argv.Add(arg);
                }
            }
            Settings["$ARGC"] = cnt;
            Settings["$ARGV"] = argv.ToArray<string>();
        }

        private static void LoadGlobalFunctions()
        {
            v8.AddHostType("CSFile", typeof(System.IO.File));
            v8.AddHostType("CSConsole", typeof(System.Console));
            v8.AddHostType("CSPath", typeof(System.IO.Path));
            v8.AddHostType("CSDirectory", typeof(System.IO.Directory));
            v8.AddHostType("CSDirectoryInfo", typeof(System.IO.DirectoryInfo));
            v8.AddHostType("CSEnvironment", typeof(System.Environment));
            v8.AddHostType("CSString", typeof(System.String));
            v8.AddHostType("CSDateTime", typeof(System.DateTime));
            v8.AddHostType("CSDebugger", typeof(System.Diagnostics.Debugger));

            v8.AddHostType("CSINI", typeof(INI));
            v8.AddHostType("CSKeyGenerator", typeof(KeyGenerator));

            v8.AddHostObject("CSX", new ExtendedHostFunctions());
            v8.AddHostObject("CS", new HostFunctions());
            v8.AddHostObject("CSClr", new HostTypeCollection("mscorlib", "System", "System.Core", "System.Data"));

            v8.AddHostObject("CSSettings", Settings);

            v8.AddHostType("CSLychen", typeof(Program)); // Experimental. No idea if useful or dangerous.
        }
    }
}
