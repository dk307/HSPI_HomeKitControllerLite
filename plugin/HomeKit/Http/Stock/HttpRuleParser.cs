// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Text;

#nullable enable

namespace System.Net.Http
{
    internal static class HttpRuleParser
    {
        private static readonly bool[] s_tokenChars = CreateTokenChars();

        internal const char CR = (char)13;
        internal const char LF = (char)10;
        internal const int MaxInt64Digits = 19;
        internal const int MaxInt32Digits = 10;

        // iso-8859-1, Western European (ISO)
        internal static readonly Encoding DefaultHttpEncoding = Encoding.GetEncoding(28591);

        private static bool[] CreateTokenChars()
        {
            // token = 1*<any CHAR except CTLs or separators>
            // CTL = <any US-ASCII control character (octets 0 - 31) and DEL (127)>

            var tokenChars = new bool[128]; // All elements default to "false".

            for (int i = 33; i < 127; i++) // Skip Space (32) & DEL (127).
            {
                tokenChars[i] = true;
            }

            // Remove separators: these are not valid token characters.
            tokenChars[(byte)'('] = false;
            tokenChars[(byte)')'] = false;
            tokenChars[(byte)'<'] = false;
            tokenChars[(byte)'>'] = false;
            tokenChars[(byte)'@'] = false;
            tokenChars[(byte)','] = false;
            tokenChars[(byte)';'] = false;
            tokenChars[(byte)':'] = false;
            tokenChars[(byte)'\\'] = false;
            tokenChars[(byte)'"'] = false;
            tokenChars[(byte)'/'] = false;
            tokenChars[(byte)'['] = false;
            tokenChars[(byte)']'] = false;
            tokenChars[(byte)'?'] = false;
            tokenChars[(byte)'='] = false;
            tokenChars[(byte)'{'] = false;
            tokenChars[(byte)'}'] = false;

            return tokenChars;
        }

        internal static bool IsTokenChar(char character)
        {
            // Must be between 'space' (32) and 'DEL' (127).
            if (character > 127)
            {
                return false;
            }

            return s_tokenChars[character];
        }

        internal static int GetTokenLength(string input, int startIndex)
        {
            if (startIndex >= input.Length)
            {
                return 0;
            }

            int current = startIndex;

            while (current < input.Length)
            {
                if (!IsTokenChar(input[current]))
                {
                    return current - startIndex;
                }
                current++;
            }
            return input.Length - startIndex;
        }

        [Pure]
        internal static bool IsToken(ReadOnlySpan<byte> input)
        {
            for (int i = 0; i < input.Length; i++)
            {
                if (!IsTokenChar((char)input[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static string GetTokenString(ReadOnlySpan<byte> input)
        {
            Debug.Assert(IsToken(input));

            return Encoding.ASCII.GetString(input.ToArray());
        }

        internal static string DateToString(DateTimeOffset dateTime)
        {
            // Format according to RFC1123; 'r' uses invariant info (DateTimeFormatInfo.InvariantInfo).
            return dateTime.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
        }
    }
}