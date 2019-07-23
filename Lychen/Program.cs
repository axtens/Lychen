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
using RestSharp;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Diagnostics;


namespace Lychen
{
    static class Program
    {
        public static V8ScriptEngine v8;
        public static Dictionary<string, object> Settings = new Dictionary<string, object>();

        static int Main(string[] args)
        {
            LoadSettingsDictionary(args);

            var v8ScriptEngineFlags = V8ScriptEngineFlags.EnableDebugging
                | V8ScriptEngineFlags.EnableRemoteDebugging
                | V8ScriptEngineFlags.DisableGlobalMembers;

            if (Settings.ContainsKey("/V8DEBUG"))
            {
                v8ScriptEngineFlags |= V8ScriptEngineFlags.AwaitDebuggerAndPauseOnStart;
                if (Settings["/V8DEBUG"].GetType() != typeof(bool))
                {
                    var psi = new ProcessStartInfo(
                        $"{Settings["/V8DEBUG"]}.exe",
                        $"{Settings["/V8DEBUG"]}\"://inspect/#devices\"")
                    {
                        CreateNoWindow = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
            }

            if (Settings.ContainsKey("/DEBUG") && Settings["/DEBUG"].GetType() == typeof(bool))
            {
                System.Diagnostics.Debugger.Launch();
            }

            v8 = new V8ScriptEngine(v8ScriptEngineFlags, 9229);

            AddSymbols();

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
                script = Settings["$ARG0"].ToString();
                if (!File.Exists(script))
                {
                    Console.WriteLine($"Script {script} not found.");
                    return 2;
                }
                ConnectoToScriptINI(script);
            }
            else
            {
                if (!hasRepl)
                {
                    Console.WriteLine("No script.");
                    return 1;
                }
                ConnectoToScriptINI("repl.INI"); // FIXME. Put this somewhere useful
            }

            SetupIncludeFunction();

            object evaluand = null;

            if (script != string.Empty)
            {
                var context = v8.Compile(File.ReadAllText(script));
                try
                {
                    evaluand = v8.Evaluate(context);
                }
                catch (Exception exception)
                {
                    if (exception is IScriptEngineException scriptException)
                    {
                        Console.WriteLine(scriptException.ErrorDetails);
                    }
                }
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
            string cmd;
            do
            {
                Console.Write(Settings["$PROMPT"]);
                cmd = Console.ReadLine();
                if (cmd == "bye")
                {
                    break;
                }
                if (fileName != string.Empty)
                {
                    File.AppendAllText(fileName, cmd + "\r\n");
                }

                object evaluand;
                try
                {
                    evaluand = v8.Evaluate(cmd);
                }
                catch (ScriptEngineException see)
                {
                    evaluand = "";
                    Console.WriteLine(see.ErrorDetails);
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
                if (evaluand == null)
                {
                    evaluand = "(null)";
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
            const console = {
                log: function () {
                  var format;
                  const args = [].slice.call(arguments);
                  if (args.length > 1) {
                    format = args.shift();
                    args.forEach(function (item) {
                      format = format.replace('%s', item);
                    });
                  } else {
                    format = args.length === 1 ? args[0] : '';
                  }
                  CS.System.Console.WriteLine(format);
                }
              };
            function include(fn) {
                if (CSFile.Exists(fn)) {
                    $SYM$.Evaluate(CSFile.ReadAllText(fn));
                } else {
                    throw fn + ' not found.';
                }
            }
            function include_once(fn) {
                if (CSFile.Exists(fn)) {
                    if (!CSSettings.ContainsKey('included_' + fn)) {
                        $SYM$.Evaluate(CSFile.ReadAllText(fn));
                        CSSettings.Add('included_' + fn, true);
                    } else {
                        throw fn + ' already included.';
                    }
                } else {
                    throw fn + ' not found.';
                }
            }".Replace("$SYM$", obfusc);

            v8.Evaluate(includeCode);
        }

        private static void ConnectoToScriptINI(string script)
        {
            var ini = new INI(Path.ChangeExtension(script, ".INI"));
            v8.AddHostObject("CSScriptINI", ini);
        }

        private static void LoadSettingsDictionary(string[] args)
        {
            var argv = new List<string>();
            var cnt = 0;
            var slashCnt = 0;
            foreach (var arg in args)
            {
                if (arg.StartsWith("/"))
                {
                    slashCnt++;
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
                    Settings[$"$ARG{cnt}"] = arg;
                    cnt++;
                    argv.Add(arg);
                }
            }
            Settings["$ARGC"] = cnt;
            Settings["$ARGV"] = argv.ToArray<string>();
            Settings["/COUNT"] = slashCnt;
            Settings["$PROMPT"] = "Lychen>";
        }

        private static void AddSymbols()
        {
            AddInternalSymbols(ref v8);
            AddHostSymbols(ref v8);
            AddSystemSymbols(ref v8);
        }

        private static void AddInternalSymbols(ref V8ScriptEngine v8)
        {
            v8.AddHostType("CSINI", typeof(INI));
            v8.AddHostType("CSKeyGenerator", typeof(KeyGenerator));
            v8.AddHostObject("CSSettings", Settings);
            v8.AddHostType("CSLychen", typeof(Program)); // Experimental. No idea if useful or dangerous.
            v8.AddHostType("CSReflection", typeof(Reflection));
            v8.AddHostType("CSExtension", typeof(Lychen.Extension));
        }

        private static void AddSystemSymbols(ref V8ScriptEngine v8)
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
        }

        private static void AddHostSymbols(ref V8ScriptEngine v8)
        {
            v8.AddHostObject("CSExtendedHost", new ExtendedHostFunctions());
            v8.AddHostObject("CSHost", new HostFunctions());
            v8.AddHostObject("CS", new HostTypeCollection("mscorlib",
                "System",
                "System.Core",
                "System.Data",
                "RestSharp",
                "WebDriver",
                "WebDriver.Support"
                ));
        }
    }
}
