// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

#nullable enable

namespace System.Net.Http.Headers
{
    internal static class KnownHeaders
    {
        public static readonly KnownHeader ContentEncoding = new("Content-Encoding", HttpHeaderType.Content, GenericHeaderParser.TokenListParser, new string[] { "gzip", "deflate" });
        public static readonly KnownHeader ContentLength = new("Content-Length", HttpHeaderType.Content, Int64NumberHeaderParser.Parser);
        public static readonly KnownHeader ContentRange = new("Content-Range", HttpHeaderType.Content, GenericHeaderParser.ContentRangeParser);
        public static readonly KnownHeader ContentType = new("Content-Type", HttpHeaderType.Content, MediaTypeHeaderParser.SingleValueParser);
        public static readonly KnownHeader Host = new("Host", HttpHeaderType.Request, GenericHeaderParser.HostParser);
        public static readonly KnownHeader KeepAlive = new("Keep-Alive");
        public static readonly KnownHeader TransferEncoding = new("Transfer-Encoding", HttpHeaderType.General, TransferCodingHeaderParser.MultipleValueParser, new string[] { "chunked" });
        public static readonly KnownHeader UserAgent = new("User-Agent", HttpHeaderType.Request, ProductInfoHeaderParser.MultipleValueParser);

        // Helper interface for making GetCandidate generic over strings, utf8, etc
        private interface IHeaderNameAccessor
        {
            int Length { get; }
            char this[int index] { get; }
        }

        private readonly struct StringAccessor : IHeaderNameAccessor
        {
            private readonly string _string;

            public StringAccessor(string s)
            {
                _string = s;
            }

            public int Length => _string.Length;
            public char this[int index] => _string[index];
        }

        // Can't use Span here as it's unsupported.
        private readonly unsafe struct BytePtrAccessor : IHeaderNameAccessor
        {
            private readonly byte* _p;
            private readonly int _length;

            public BytePtrAccessor(byte* p, int length)
            {
                _p = p;
                _length = length;
            }

            public int Length => _length;
            public char this[int index] => (char)_p[index];
        }

        // Find possible known header match via lookup on length and a distinguishing char for that length.
        // Matching is case-insenstive.
        // NOTE: Because of this, we do not preserve the case of the original header,
        // whether from the wire or from the user explicitly setting a known header using a header name string.
        private static KnownHeader? GetCandidate<T>(T key)
            where T : struct, IHeaderNameAccessor     // Enforce struct for performance
        {
            int length = key.Length;
            switch (length)
            {
                case 4:
                    switch (key[0])
                    {
                        case 'H': case 'h': return Host; // [H]ost
                    }
                    break;

                case 10:
                    switch (key[0])
                    {
                        case 'K': case 'k': return KeepAlive;  // [K]eep-Alive
                        case 'U': case 'u': return UserAgent;  // [U]ser-Agent
                    }
                    break;

                case 12:
                    switch (key[2])
                    {
                        case 'N': case 'n': return ContentType; // Co[n]tent-Type
                    }
                    break;

                case 13:
                    switch (key[6])
                    {
                        case 'T': case 't': return ContentRange;  // Conten[t]-Range
                    }
                    break;

                case 14:
                    switch (key[0])
                    {
                        case 'C': case 'c': return ContentLength; // [C]ontent-Length
                    }
                    break;

                case 16:
                    switch (key[11])
                    {
                        case 'O': case 'o': return ContentEncoding; // Content-Enc[o]ding
                    }
                    break;

                case 17:
                    switch (key[0])
                    {
                        case 'T': case 't': return TransferEncoding; // [T]ransfer-Encoding
                    }
                    break;
            }

            return null;
        }

        internal static KnownHeader? TryGetKnownHeader(string name)
        {
            var candidate = GetCandidate(new StringAccessor(name));
            if (candidate != null && StringComparer.OrdinalIgnoreCase.Equals(name, candidate.Name))
            {
                return candidate;
            }

            return null;
        }

        internal static unsafe KnownHeader? TryGetKnownHeader(ReadOnlySpan<byte> name)
        {
            fixed (byte* p = &MemoryMarshal.GetReference(name))
            {
                var candidate = GetCandidate(new BytePtrAccessor(p, name.Length));
                if (candidate != null && ByteArrayHelpers.EqualsOrdinalAsciiIgnoreCase(candidate.Name, name))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}