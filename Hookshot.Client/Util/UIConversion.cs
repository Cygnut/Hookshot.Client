using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace Hookshot.Client.Util
{
    public static class UIConversion
    {
        static readonly string[] ByteUnits = new string[] { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        public static string FromBytes(long bytes)
        {
            if (bytes == 0) return "0 B";
            var k = 1000; // or 1024 for binary
            var i = (int)Math.Floor(Math.Log(bytes) / Math.Log(k));
            return (bytes / Math.Pow(k, i)).ToString("F3") + ' ' + ByteUnits[i];
        }

        public static DateTime FromMilliseconds(long unixTime)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddMilliseconds(unixTime);
        }
    }
}