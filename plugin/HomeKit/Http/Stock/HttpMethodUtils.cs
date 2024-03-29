﻿using System.Collections.Generic;
using System.Reflection;

#nullable enable

namespace System.Net.Http
{
    internal static class HttpMethodUtils
    {
        [Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3963:\"static\" fields should be initialized inline", Justification = "<Pending>")]
        [Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields", Justification = "<Pending>")]
        static HttpMethodUtils()
        {
            PropertyInfo connectProperty = typeof(HttpMethod).GetProperty("Connect", BindingFlags.Static | BindingFlags.NonPublic);
            PropertyInfo patchProperty = typeof(HttpMethod).GetProperty("Patch", BindingFlags.Static | BindingFlags.NonPublic);
            if (connectProperty == null || patchProperty == null)
            {
                // .NET Framework
                s_connectMethod = new HttpMethod("CONNECT");
                s_patchMethod = new HttpMethod("PATCH");
            }
            else
            {
                s_connectMethod = (HttpMethod)connectProperty.GetValue(null);
                s_patchMethod = (HttpMethod)patchProperty.GetValue(null);
            }
            s_knownMethods.Add(s_connectMethod, s_connectMethod);
            s_knownMethods.Add(s_patchMethod, s_patchMethod);
        }

        public static HttpMethod Connect
        {
            get { return s_connectMethod; }
        }

        internal static HttpMethod Normalize(HttpMethod method)
        {
            return s_knownMethods.TryGetValue(method, out HttpMethod normalized) ? normalized : method;
        }

        private static readonly HttpMethod s_connectMethod;

        private static readonly Dictionary<HttpMethod, HttpMethod> s_knownMethods = new(9)
        {
            { HttpMethod.Get, HttpMethod.Get },
            { HttpMethod.Put, HttpMethod.Put },
            { HttpMethod.Post, HttpMethod.Post },
            { HttpMethod.Delete, HttpMethod.Delete },
            { HttpMethod.Head, HttpMethod.Head },
            { HttpMethod.Options, HttpMethod.Options },
            { HttpMethod.Trace, HttpMethod.Trace }
        };

        private static readonly HttpMethod s_patchMethod;
    }
}