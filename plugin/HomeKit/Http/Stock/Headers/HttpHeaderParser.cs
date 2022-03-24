// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Contracts;

#nullable enable

namespace System.Net.Http.Headers
{
    internal abstract class HttpHeaderParser
    {
        protected HttpHeaderParser(bool supportsMultipleValues)
        {
            _supportsMultipleValues = supportsMultipleValues;

            if (supportsMultipleValues)
            {
                _separator = DefaultSeparator;
            }
        }

        protected HttpHeaderParser(bool supportsMultipleValues, string separator)
        {
            Debug.Assert(!string.IsNullOrEmpty(separator));

            _supportsMultipleValues = supportsMultipleValues;
            _separator = separator;
        }

        public string? Separator => _separator;

        public bool SupportsMultipleValues => _supportsMultipleValues;

        // If ValueType is a custom header value type (e.g. NameValueHeaderValue) it already implements ToString() correctly.
        // However for existing types like int, byte[], DateTimeOffset we can't override ToString(). Therefore the 
        // parser provides a ToString() virtual method that can be overridden by derived types to correctly serialize 
        // values (e.g. byte[] to Base64 encoded string).
        public virtual string? ToString(object value)
        {
            return value?.ToString();
        }

        internal const string DefaultSeparator = ", ";

        protected bool _supportsMultipleValues;
        private readonly string? _separator;
    }
}
