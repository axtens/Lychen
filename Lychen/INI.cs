using System.Runtime.InteropServices;
using System.Text;
using NLog;

namespace Lychen
{
    /// <summary>
    ///     Create a New INI file to store or load data
    /// </summary>
    public class INI
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public string path;

        /// <summary>
        ///     INIFile Constructor.
        /// </summary>
        /// <param name="INIPath"></param>
        public INI(string INIPath)
        {
            path = INIPath;
        }

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal,
            int size, string filePath);

        /// <summary>
        ///     Write Data to the INI File
        /// </summary>
        /// <param name="Section"></param>
        /// Section name
        /// <param name="Key"></param>
        /// Key Name
        /// <param name="Value"></param>
        /// Value Name
        public void IniWriteValue(string Section, string Key, string Value)
        {
            WritePrivateProfileString(Section, Key, Value, path);
        }

        /// <summary>
        ///     Read Data Value From the Ini File
        /// </summary>
        /// <param name="Section"></param>
        /// <param name="Key"></param>
        /// <param name="Path"></param>
        /// <returns></returns>
        public string IniReadValue(string Section, string Key, string DefaultValue)
        {
            var temp = new StringBuilder(1024);
            var i = GetPrivateProfileString(Section, Key, "", temp, 1024, path);
            if (i != 0)
                return temp.ToString();
            return DefaultValue;
        }
    }
}