﻿using System;
using System.Linq;

namespace Urlscan
{
    internal static class Extensions
    {
        public static DateTime ToDate(this long value)
        {
            return DateTime.UnixEpoch.AddSeconds(value);
        }

        public static long ToUnixSeconds(this DateTime value)
        {
            return (long)(value - DateTime.UnixEpoch).TotalSeconds;
        }

        public static string ToKebabCase(this string str) =>
            string.Concat(str.Select((x, i) => i > 0 && char.IsUpper(x) ? "-" + x.ToString() : x.ToString())).ToLower();
    }
}