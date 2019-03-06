if (CSSettings("$ARGC") < 2) {
  CSConsole.WriteLine("{0} logfile", CSSettings("$ARG0"));
  CSEnvironment.Exit(1);
}

if (CSSettings.ContainsKey("/" + CSPath.GetFileNameWithoutExtension(CSSettings("$ARG0")))) {
  debugger;
}

var logfile = CSSettings("$ARG1");

var data = CSFile.ReadAllText(logfile).split(/\r\n/g);
for (var i = 0; i < (data.length - 1); i++) {
  var line = data[i].split("\t");
  var linep = data[i + 1].split("\t");
  var date,
    datep;
  try {
    date = CS.System.Convert.ToDateTime(line[0]);
  } catch (E) {
    continue;
  }
  try {
    datep = CS.System.Convert.ToDateTime(linep[0]);
  } catch (E) {
    continue;
  }
  var span = datep.Subtract(date);
  var comp = CS.System.DateTime.Compare(date, datep);
  if (comp !== 0) {
    CSConsole.WriteLine("{0}\t{1}\t{2}", span.TotalMinutes, linep[3], linep[4]);
  }
}
""