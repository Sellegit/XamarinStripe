using System;
using PCLWebUtility;

namespace System.Web {
    public static class HttpUtility {
        public static string UrlEncode (string s) {
            if (s == null) {
                return "";
            } else {
                return WebUtility.UrlEncode (s);
            }
        }
    }
}

