if (!CSSettings.ContainsKey("$ARG1")) {
	CSConsole.WriteLine("Syntax:\n\t{0} filename.ext\n\nOutputs filename.json", CSSettings("$ARG0"));
	CSEnvironment.Exit(1);
}

var ifile = CSSettings("$ARG1");
var ofile = CSPath.ChangeExtension(ifile, ".json");

var recs = [];
var data = CSFile.ReadAllText(ifile).split(/\r\n|\r|\n/g);
var heads = data[0].split("\t");
for (var d = 1; d < data.length; d++) {
	var line = data[d].split("\t");
	var rec = {};
	for (var h = 0; h < heads.length; h++) {
		rec[heads[h]] = line[h];
	}
	recs.push(rec);
}
CSFile.WriteAllText(ofile, JSON.stringify(recs));