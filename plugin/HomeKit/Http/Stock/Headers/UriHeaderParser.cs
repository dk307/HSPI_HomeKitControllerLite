// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;

namespace System.Net.Http.Headers
{
    // Don't derive from BaseHeaderParser since parsing is delegated to Uri.TryCreate()
    // which will remove leading and trailing whitespace.
    internal class UriHeaderParser : HttpHeaderParser
    {
        internal static readonly UriHeaderParser RelativeOrAbsoluteUriParser =
            new(UriKind.RelativeOrAbsolute);

        private UriHeaderParser(UriKind uriKind)
            : base(false)
        {
        }

        // The normal client header parser just casts bytes to chars (see GetString).
        // Check if those bytes were actually utf-8 instead of ASCII.
        // If not, just return the input value.
        internal static string DecodeUtf8FromString(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input;
            }

            bool possibleUtf8 = false;
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] > (char)255)
                {
                    return input; // This couldn't have come from the wire, someone assigned it directly.
                }
                else if (input[i] > (char)127)
                {
                    possibleUtf8 = true;
                    break;
                }
            }
            if (possibleUtf8)
            {
                byte[] rawBytes = new byte[input.Length];
                for (int i = 0; i < input.Length; i++)
                {
                    if (input[i] > (char)255)
                    {
                        return input; // This couldn't have come from the wire, someone assigned it directly.
                    }
                    rawBytes[i] = (byte)input[i];
                }
                try
                {
                    // We don't want '?' replacement characters, just fail.

                    var decoder = Encoding.GetEncoding("utf-8", EncoderFallback.ExceptionFallback,
                        DecoderFallback.ExceptionFallback);

                    return decoder.GetString(rawBytes, 0, rawBytes.Length);
                }
                catch (ArgumentException)
                {
                    // Not actually Utf-8
                }
            }
            return input;
        }

        public override string ToString(object value)
        {
            Debug.Assert(value is Uri);
            Uri uri = (Uri)value;

            if (uri.IsAbsoluteUri)
            {
                return uri.AbsoluteUri;
            }
            else
            {
                return uri.GetComponents(UriComponents.SerializationInfoString, UriFormat.UriEscaped);
            }
        }
    }
}