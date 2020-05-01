namespace Rpi.Common.Extensions
{
    public static class IntExtension
    {
        /// <summary>
        /// Returns true if number is between min and max (inclusive), false if outside.
        /// </summary>
        public static bool Between(this int s, int min, int max)
        {
            if (s < min)
                return false;
            if (s > max)
                return false;
            return true;
        }
    }
}
