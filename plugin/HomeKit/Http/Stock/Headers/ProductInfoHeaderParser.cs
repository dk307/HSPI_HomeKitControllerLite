// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace System.Net.Http.Headers
{
    // Don't derive from BaseHeaderParser since empty values are not supported. After a ' ' separator a valid value
    // must follow. Also leading separators are not allowed.
    internal class ProductInfoHeaderParser : HttpHeaderParser
    {
        private ProductInfoHeaderParser(bool supportsMultipleValues)
                    : base(supportsMultipleValues, separator)
        {
        }

        internal static readonly ProductInfoHeaderParser MultipleValueParser = new(true);

        internal static readonly ProductInfoHeaderParser SingleValueParser = new(false);

        // Unlike most other headers, User-Agent and Server use whitespace as separators
        private const string separator = " ";
    }
}