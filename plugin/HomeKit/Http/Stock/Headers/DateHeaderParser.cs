// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Net.Http.Headers
{
    // Don't derive from BaseHeaderParser since parsing is delegated to DateTimeOffset.TryParseExact()
    // which will remove leading, trailing, and whitespace in the middle of the string.
    internal class DateHeaderParser : HttpHeaderParser
    {
        private DateHeaderParser()
            : base(false)
        {
        }

        public override string ToString(object value)
        {
            return HttpRuleParser.DateToString((DateTimeOffset)value);
        }

        internal static readonly DateHeaderParser Parser = new();
    }
}