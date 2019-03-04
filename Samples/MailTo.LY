debugger;

if (CSSettings("/COUNT") === 0 || !CSSettings.ContainsKey("/T") || !CSSettings.ContainsKey("/S") || !CSSettings.ContainsKey("/A")) {
  CSConsole.WriteLine("{0}\r\nSyntax:\r\n\t/T:<to> /S:<subj> /A:<attfile>", CSSettings("$ARG0"));
  CSEnvironment.Exit(1);
}

var mail = CSClr.System.Net.Mail;
var message = new mail.MailMessage();

message.From = new mail.MailAddress(
    CSScriptINI.IniReadValue("Settings", "ReplyToEmail", ""),
    CSScriptINI.IniReadValue("Settings", "ReplyToName", ""));

message.To.Add(
  new mail.MailAddress(
    CSSettings("/T")));

message.Body =
  "See attachment: " + CSSettings("/A");

message.Subject = CSSettings("/S");

var att;
if (CSFile.Exists(CSSettings("/A"))) {
  att = new mail.Attachment(CSSettings("/A"), "application/octet-stream");
  message.Attachments.Add(att);
}

var cred = new CSClr.System.Net.NetworkCredential(
    CSScriptINI.IniReadValue("Settings", "Account", ""),
    CSScriptINI.IniReadValue("Settings", "Password", ""));
	
var smtp = new mail.SmtpClient(
    CSScriptINI.IniReadValue("Settings", "Postoffice", ""),
    parseInt(CSScriptINI.IniReadValue("Settings", "Port", "25"), 10));
	
smtp.Credentials = cred;
smtp.EnableSsl = CSScriptINI.IniReadValue("Settings", "SSL", "N") === "Y";
smtp.Send(message);
