using System;

namespace Rpi.Extensions
{
    public static class TimeSpanExtension
    {
        /// <summary>
        /// Renders shorter, more display-friendly version of a timespan.  Useful for ETA.
        /// </summary>
        public static string ToShortString(this TimeSpan input)
        {
            string value = input.ToString();
            if (value.LastIndexOf('.') != -1)
                value = value.Remove(value.LastIndexOf('.'));
            return value;
        }

        /// <summary>
        /// Generates a sortable string representation of the date portion of the specified value (example: YYYYMMDD).
        /// </summary>
        public static string ToSortDate(this DateTime input)
        {
            string value = input.ToString("yyyyMMdd");
            return value;
        }

        /// <summary>
        /// Generates a sortable string representation of the date and time portion of the specified value (example: YYYYMMDD_HHMMSS).
        /// </summary>
        public static string ToSortDateTime(this DateTime input)
        {
            string value = input.ToString("yyyyMMdd-HHmmss");
            return value;
        }

        /// <summary>
        /// Generates simple but relevant output for a duration, used for console output, etc.
        /// Needs some work.
        /// </summary>
        public static string ToFriendlyDuration(this TimeSpan input)
        {
            string value = "";

            if (input.TotalSeconds < 1)
                value = ((Int32)input.TotalMilliseconds) + "ms";

            else if (input.TotalMinutes < 1)
                value = ((Int32)input.TotalSeconds) + "sec";

            else if (input.TotalHours < 1)
                value = ((Int32)input.TotalMinutes) + "min";

            else if (input.TotalDays < 1)
                value = ((Int32)input.TotalHours) + "hrs";

            else
                value = ((Int32)input.TotalDays) + "days";

            return value;
        }
    }
}
