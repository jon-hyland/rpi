using System;

namespace Rpi.Common.Helpers
{
    public static class EnumHelper
    {
        /// <summary>
        /// Converts string to enum value of specified T type.
        /// </summary>
        public static T FromString<T>(string str, bool ignoreCase = false) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
                throw new ArgumentException("T must be an enumerated type");
            if (Enum.TryParse<T>(str, ignoreCase, out T result))
                return result;
            throw new ArgumentException("String is not valid enum value");
        }
    }
}
