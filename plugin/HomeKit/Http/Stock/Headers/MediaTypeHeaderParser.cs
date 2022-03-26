// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace System.Net.Http.Headers
{
    internal class MediaTypeHeaderParser : BaseHeaderParser
    {
        internal static readonly MediaTypeHeaderParser SingleValueParser = new(false);

        private MediaTypeHeaderParser(bool supportsMultipleValues)
            : base(supportsMultipleValues)
        {
            _supportsMultipleValues = supportsMultipleValues;
        }
    }
}