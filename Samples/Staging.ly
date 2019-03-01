var stagingINI = CSPath.GetFullPath("Staging.INI");

if (CSSettings.ContainsKey("/D")) {
  debugger;
}

if (!CSFile.Exists(stagingINI)) {
  CSConsole.WriteLine(stagingINI + " not found.");
  CSEnvironment.Exit(1);
}

var INI = new CSINI(stagingINI);

var destination = INI.IniReadValue("Settings", "Destination", "");
if ("" === destination) {
  CSConsole.WriteLine(stagingINI + " has no [Settings]Destination.");
  CSEnvironment.Exit(2);
}

var count = INI.IniReadValue("Settings", "Count", "0");
if (parseInt(count, 10) === 0) {
  CSConsole.WriteLine(stagingINI + " has no [Settings]Count.");
  CSEnvironment.Exit(2);
}

var fileList = [];
for (var i = 1; i <= parseInt(count, 10); i++) {
  var fileItem = INI.IniReadValue("Item." + i, "Name", "");
  if (fileItem !== "") {
    fileList.push(fileItem);
  }
}

CleanOutDestination(destination);

for (var i = 0; i < fileList.length; i++) {
  var sourceFile = fileList[i];
  CSConsole.Write(Array(i + 1).join("*") + "\r");
  var destFile = CSPath.Combine(destination, sourceFile);
  if (!CSFile.Exists(destFile)) {
    CSFile.Copy(sourceFile, destFile, true);
    CSConsole.WriteLine("\tCopied {0} to {1}", sourceFile, destination);
  } else {
    var sourceDate = CSFile.GetLastWriteTime(sourceFile);
    var destDate = CSFile.GetLastWriteTime(destFile);
    if (CSDateTime.Compare(sourceDate, destDate) > 0) {
      CSFile.Copy(sourceFile, destFile, true);
      CSConsole.WriteLine("\tUpdated {0} to {1}", sourceFile, destination);
    }
  }
}

function CleanOutDestination(destination) {
  var extensions = ["exe", "pdb", "config", "dll", "xml", "md5"];

  for (var e = 0; e < extensions.length; e++) {
    var ext = extensions[e];
    var matching = CSDirectory.GetFiles(destination, "*." + ext);
    for (var m = 0; m < matching.Length; m++) {
      CSFile.Delete(matching[m]);
    }
  }
}