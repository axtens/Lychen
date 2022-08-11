using System.Security.Cryptography;
using System.Text;
using NLog;

// https://stackoverflow.com/questions/1344221/how-can-i-generate-random-alphanumeric-strings

namespace Lychen
{
    public static class KeyGenerator
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static string GetUniqueKey(int size)
        {
            var chars =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();
            var data = new byte[size];
            using (var crypto = new RNGCryptoServiceProvider())
            {
                crypto.GetBytes(data);
            }

            var result = new StringBuilder(size);
            foreach (var b in data) result.Append(chars[b % chars.Length]);
            return result.ToString();
        }
    }
}