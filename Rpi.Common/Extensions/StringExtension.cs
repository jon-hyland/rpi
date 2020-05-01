using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Rpi.Common.Extensions
{
    /// <summary>
    /// Extends .NET's String class, mostly for generating a more accurate hash code.
    /// </summary>
    public static class StringExtension
    {
        //private
        private static readonly MD5 _md5 = new MD5CryptoServiceProvider();

        /// <summary>
        /// Return X characters from the left of the string.
        /// </summary>
        public static string Left(this string s, int count)
        {
            return s.Substring(0, count);
        }

        /// <summary>
        /// Return X characters from the right of the string.
        /// </summary>
        public static string Right(this string s, int count)
        {
            return s.Substring(s.Length - count, count);
        }

        /// <summary>
        /// Convert string to integer.
        /// </summary>
        public static int ToInteger(this string s)
        {
            Int32.TryParse(s, out int integerValue);
            return integerValue;
        }

        /// <summary>
        /// Returns true if string is integer.
        /// </summary>
        public static bool IsInteger(this string s)
        {
            Regex regularExpression = new Regex("^-[0-9]+$|^[0-9]+$");
            return regularExpression.Match(s).Success;
        }

        /// <summary>
        /// Gets a more accurate hash code than String.GetHashCode() provides.
        /// </summary>
        public static long GetLongHash(this string s)
        {
            long hash = GenerateLongHashUsingMD5(s);
            return hash;
        }

        /// <summary>
        /// Returns true if any character in string is considered white space.
        /// </summary>
        public static bool HasWhiteSpace(this string s)
        {
            if (s == null)
                throw new ArgumentNullException("s");

            for (int i = 0; i < s.Length; i++)
            {
                if (Char.IsWhiteSpace(s[i]))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Converts the first letter of each word to upper case, leaving words with all upper.
        /// </summary>
        public static string ToTitleCase(this string s)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s);
        }

        /// <summary>
        /// Returns the number of occurances of the specified substring.
        /// </summary>
        public static int CountOccurances(this string s, string Substring)
        {
            return Regex.Matches(s, Substring).Count;
        }

        /// <summary>
        /// Generate long hash code, using part of an MD5 hash.
        /// </summary>
        private static long GenerateLongHashUsingMD5(string Input)
        {
            byte[] inputBytes = Encoding.Unicode.GetBytes(Input ?? String.Empty);
            byte[] outputBytes = null;
            lock (_md5)
            {
                outputBytes = _md5.ComputeHash(inputBytes);
            }
            long hash = BitConverter.ToInt64(outputBytes, 7);
            return hash;
        }

        /// <summary>
        /// Returns URL-encoded version of the string.
        /// </summary>
        public static string UrlEncode(this string s)
        {
            return HttpUtility.UrlEncode(s);
        }

        /// <summary>
        /// Returns HTML-encoded version of the string.
        /// </summary>
        public static string HtmlEncode(this string s)
        {
            return HttpUtility.HtmlEncode(s);
        }

        /// <summary>
        /// Returns URL-decoded version of the string.
        /// </summary>
        public static string UrlDecode(this string s)
        {
            return HttpUtility.UrlDecode(s);
        }

        /// <summary>
        /// Returns HTML-decoded version of the string.
        /// </summary>
        public static string HtmlDecode(this string s)
        {
            return HttpUtility.HtmlDecode(s);
        }

        /// <summary>
        /// Replaces, when possible, diacritic versions of Latin characters with the non-diacritic versions.
        /// Example: "é" to "e".
        /// </summary>
        public static string RemoveDiacritics(this string s)
        {
            string norm = s.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (var c in norm)
            {
                UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

    }
}
