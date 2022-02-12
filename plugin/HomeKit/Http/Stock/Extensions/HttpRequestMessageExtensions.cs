using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;

#nullable enable

namespace System.Net.Http
{
    internal static class HttpRequestMessageExtensions
    {
        [Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields", Justification = "<Pending>")]
        [Diagnostics.CodeAnalysis.SuppressMessage("Major Bug", "S2259:Null pointers should not be dereferenced", Justification = "<Pending>")]
        public static bool HasHeaders(this HttpRequestMessage request)
        {
            // Note: The field name is _headers in .NET core
            bool isDotNetFramework = IsDotNetFramework();
            string headersFieldName = isDotNetFramework ? "headers" : "_headers";
            FieldInfo headersField = typeof(HttpRequestMessage).GetField(headersFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (headersField == null && isDotNetFramework)
            {
                // Fallback for .NET Framework 4.6.1
                headersFieldName = "_headers";
                headersField = typeof(HttpRequestMessage).GetField(headersFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            }
            var headers = (HttpRequestHeaders?)headersField?.GetValue(request);
            return headers != null;
        }

        public static bool IsDotNetFramework()
        {
            const string DotnetFrameworkDescription = ".NET Framework";
            string frameworkDescription = RuntimeInformation.FrameworkDescription;
            return frameworkDescription.StartsWith(DotnetFrameworkDescription);
        }
    }
}