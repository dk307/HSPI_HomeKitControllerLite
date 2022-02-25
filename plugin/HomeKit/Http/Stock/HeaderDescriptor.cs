// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

#nullable enable

namespace System.Net.Http.Headers
{
    internal readonly struct HeaderDescriptor : IEquatable<HeaderDescriptor>
    {
        private readonly string _headerName;
        private readonly KnownHeader? _knownHeader;

        public HeaderDescriptor(KnownHeader knownHeader)
        {
            _knownHeader = knownHeader;
            _headerName = knownHeader.Name;
        }

        // This should not be used directly; use static TryGet below
        private HeaderDescriptor(string headerName)
        {
            _headerName = headerName;
            _knownHeader = null;
        }

        public string Name => _headerName;
        public HttpHeaderParser? Parser => _knownHeader?.Parser;
        public HttpHeaderType HeaderType => _knownHeader == null ? HttpHeaderType.Custom : _knownHeader.HeaderType;
        public KnownHeader? KnownHeader => _knownHeader;

        [Diagnostics.CodeAnalysis.SuppressMessage("Blocker Code Smell", "S3877:Exceptions should not be thrown from unexpected methods", Justification = "<Pending>")]
        public override bool Equals(object obj) => throw new InvalidOperationException();   // Ensure this is never called, to avoid boxing

        public bool Equals(HeaderDescriptor other) =>
            _knownHeader == null ?
                string.Equals(_headerName, other._headerName, StringComparison.OrdinalIgnoreCase) :
                _knownHeader == other._knownHeader;

        public override int GetHashCode() => _knownHeader?.GetHashCode() ?? StringComparer.OrdinalIgnoreCase.GetHashCode(_headerName);

        public static bool operator ==(HeaderDescriptor left, HeaderDescriptor right) => left.Equals(right);

        public static bool operator !=(HeaderDescriptor left, HeaderDescriptor right) => !left.Equals(right);

        // Returns false for invalid header name.
        public static bool TryGet(ReadOnlySpan<byte> headerName, out HeaderDescriptor descriptor)
        {
            Debug.Assert(headerName.Length > 0);

            var knownHeader = KnownHeaders.TryGetKnownHeader(headerName);
            if (knownHeader != null)
            {
                descriptor = new HeaderDescriptor(knownHeader);
                return true;
            }

            if (!HttpRuleParser.IsToken(headerName))
            {
                descriptor = default;
                return false;
            }

            descriptor = new HeaderDescriptor(HttpRuleParser.GetTokenString(headerName));
            return true;
        }

        public HeaderDescriptor AsCustomHeader()
        {
            return new HeaderDescriptor(_knownHeader.Name);
        }

        public string GetHeaderValue(ReadOnlySpan<byte> headerValue)
        {
            if (headerValue.Length == 0)
            {
                return string.Empty;
            }

            // If it's a known header value, use the known value instead of allocating a new string.
            if (_knownHeader != null && _knownHeader.KnownValues != null)
            {
                string[] knownValues = _knownHeader.KnownValues;
                for (int i = 0; i < knownValues.Length; i++)
                {
                    if (ByteArrayHelpers.EqualsOrdinalAsciiIgnoreCase(knownValues[i], headerValue))
                    {
                        return knownValues[i];
                    }
                }
            }

            return HttpRuleParser.DefaultHttpEncoding.GetString(headerValue.ToArray());
        }
    }
}