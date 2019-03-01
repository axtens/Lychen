if (CSSettings.ContainsKey("/D")) {
  debugger;
}

if (CSSettings("$ARGC") <= 1) {
  CSConsole.WriteLine("Please specify staging directory");
  CSEnvironment.Exit(1);
}

var list = CSDirectory.GetFiles(".\\", "*.*");
var shortList = [];
for (var i = 0; i < list.Length; i++) {
  if (list[i].match(/\.dll$|\.exe$|\.pdb$|\.config$/i) !== null) {
    if (list[i].match(/vshost/i) === null) {
      shortList.push(list[i])
    }
  }
}

var stagingINI = CSPath.GetFullPath("Staging.INI");
CSFile.WriteAllText(stagingINI, "");

var ini = new CSINI(stagingINI);


ini.IniWriteValue("Settings", "Destination", CSSettings("$ARG2"));
ini.IniWriteValue("Settings", "Count", String(shortList.length));

for (i = 0; i < shortList.length; i++) {
  ini.IniWriteValue("Item." + String(i + 1), "Name", shortList[i]);
}

CSConsole.WriteLine(shortList.length + " items recorded");