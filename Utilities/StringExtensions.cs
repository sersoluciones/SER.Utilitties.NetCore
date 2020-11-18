using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Utilities
{
    public static class StringExtensions
    {
        private static readonly Random random = new Random();
        public static string ToSnakeCase(this string input)
        {
            if (string.IsNullOrEmpty(input)) { return input; }

            var startUnderscores = Regex.Match(input, @"^_+");
            return startUnderscores + Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1_$2").ToLower();
        }

        public static string ToCamelCase(this string s1)
        {
            // return Regex.Replace(s1,).ToLower();
            return Regex.Replace(s1, "_[a-z]", delegate (Match m) {
                return m.ToString().TrimStart('_').ToUpper();
            }).FirstCharToUpper();
        }

        public static string FirstCharToUpper(this string input)
        {
            switch (input)
            {
                case null: throw new ArgumentNullException(nameof(input));
                case "": throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input));
                default: return input.First().ToString().ToUpper() + input.Substring(1);
            }
        }

        public static string ToSlug(this string value)
        {
            if (value == null) { return null; }

            return Regex.Replace(value.ToString(),
                                 "([a-z])([A-Z])",
                                 "$1-$2",
                                 RegexOptions.CultureInvariant,
                                 TimeSpan.FromMilliseconds(100)).ToLowerInvariant();
        }

        public static string GenerateSlug(this string phrase)
        {
            string str = phrase.RemoveAccent().ToLower();
            // invalid chars           
            str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
            // convert multiple spaces into one space   
            str = Regex.Replace(str, @"\s+", " ").Trim();
            // cut and trim 
            str = str.Substring(0, str.Length <= 45 ? str.Length : 45).Trim();
            str = Regex.Replace(str, @"\s", "-"); // hyphens   
            return str;
        }

        public static string RemoveAccent(this string txt)
        {
            byte[] bytes = System.Text.Encoding.GetEncoding("Cyrillic").GetBytes(txt);
            return System.Text.Encoding.ASCII.GetString(bytes);
        }

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var txt = new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
            return txt;
        }

        public static string RandomLettersString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var txt = new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
            return txt;
        }

        public static string ComputeSha256Hash(this string rawData)
        {
            // Create a SHA256   
            using SHA256 sha256Hash = SHA256.Create();
            // ComputeHash - returns byte array  
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            // Convert byte array to a string   
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }

        public static string GetRandomString(int length)
        {
            const string CHARS = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890_";
            var sb = new StringBuilder();
            var rnd = new Random();
            while (sb.Length < length)
            {
                sb.Append(CHARS[rnd.Next(CHARS.Length)]);
            }
            return sb.ToString();

        }

        /// <summary>
        /// create aes key 256 bits
        /// </summary>
        /// <param name="password"></param>
        /// <param name="keyBytes"></param>
        /// <returns></returns>
        public static byte[] CreateKey(this string password, int keyBytes = 32)
        {
            const int Iterations = 300;
            var keyGenerator = new Rfc2898DeriveBytes(password, Salt, Iterations);
            return keyGenerator.GetBytes(keyBytes);
        }

        private static readonly byte[] Salt = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 };

        /// <summary>
        /// Encode base64
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
        public static string Base64Encode(this string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        /// <summary>
        /// Decode base 64
        /// </summary>
        /// <param name="base64EncodedData"></param>
        /// <returns></returns>
        public static string Base64Decode(this string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }

        public static ValueTask<List<TSource>> EnumerableToListAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default)
        {
            return Core(source, cancellationToken);

            static async ValueTask<List<TSource>> Core(IAsyncEnumerable<TSource> source, CancellationToken cancellationToken)
            {
                var list = new List<TSource>();

                await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    list.Add(item);
                }

                return list;
            }
        }
    }
}