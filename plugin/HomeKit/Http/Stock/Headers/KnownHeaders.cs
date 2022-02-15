// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace System.Net.Http.Headers
{
    internal static class KnownHeaders
    {
        // If you add a new entry here, you need to add it to TryGetKnownHeader below as well.

        public static readonly KnownHeader Accept = new("Accept", HttpHeaderType.Request, MediaTypeHeaderParser.MultipleValuesParser);
        public static readonly KnownHeader AcceptCharset = new("Accept-Charset", HttpHeaderType.Request, GenericHeaderParser.MultipleValueStringWithQualityParser);
        public static readonly KnownHeader AcceptEncoding = new("Accept-Encoding", HttpHeaderType.Request, GenericHeaderParser.MultipleValueStringWithQualityParser);
        public static readonly KnownHeader AcceptLanguage = new("Accept-Language", HttpHeaderType.Request, GenericHeaderParser.MultipleValueStringWithQualityParser);
        public static readonly KnownHeader AcceptPatch = new("Accept-Patch");
        public static readonly KnownHeader AcceptRanges = new("Accept-Ranges", HttpHeaderType.Response, GenericHeaderParser.TokenListParser);
        public static readonly KnownHeader AccessControlAllowCredentials = new("Access-Control-Allow-Credentials");
        public static readonly KnownHeader AccessControlAllowHeaders = new("Access-Control-Allow-Headers");
        public static readonly KnownHeader AccessControlAllowMethods = new("Access-Control-Allow-Methods");
        public static readonly KnownHeader AccessControlAllowOrigin = new("Access-Control-Allow-Origin");
        public static readonly KnownHeader AccessControlExposeHeaders = new("Access-Control-Expose-Headers");
        public static readonly KnownHeader AccessControlMaxAge = new("Access-Control-Max-Age");
        public static readonly KnownHeader Age = new("Age", HttpHeaderType.Response, TimeSpanHeaderParser.Parser);
        public static readonly KnownHeader Allow = new("Allow", HttpHeaderType.Content, GenericHeaderParser.TokenListParser);
        public static readonly KnownHeader AltSvc = new("Alt-Svc");
        public static readonly KnownHeader Authorization = new("Authorization", HttpHeaderType.Request, GenericHeaderParser.SingleValueAuthenticationParser);
        public static readonly KnownHeader CacheControl = new("Cache-Control", HttpHeaderType.General, CacheControlHeaderParser.Parser);
        public static readonly KnownHeader Connection = new("Connection", HttpHeaderType.General, GenericHeaderParser.TokenListParser, new string[] { "close" });
        public static readonly KnownHeader ContentDisposition = new("Content-Disposition", HttpHeaderType.Content, GenericHeaderParser.ContentDispositionParser);
        public static readonly KnownHeader ContentEncoding = new("Content-Encoding", HttpHeaderType.Content, GenericHeaderParser.TokenListParser, new string[] { "gzip", "deflate" });
        public static readonly KnownHeader ContentLanguage = new("Content-Language", HttpHeaderType.Content, GenericHeaderParser.TokenListParser);
        public static readonly KnownHeader ContentLength = new("Content-Length", HttpHeaderType.Content, Int64NumberHeaderParser.Parser);
        public static readonly KnownHeader ContentLocation = new("Content-Location", HttpHeaderType.Content, UriHeaderParser.RelativeOrAbsoluteUriParser);
        public static readonly KnownHeader ContentMD5 = new("Content-MD5", HttpHeaderType.Content, ByteArrayHeaderParser.Parser);
        public static readonly KnownHeader ContentRange = new("Content-Range", HttpHeaderType.Content, GenericHeaderParser.ContentRangeParser);
        public static readonly KnownHeader ContentSecurityPolicy = new("Content-Security-Policy");
        public static readonly KnownHeader ContentType = new("Content-Type", HttpHeaderType.Content, MediaTypeHeaderParser.SingleValueParser);
        public static readonly KnownHeader Cookie = new("Cookie");
        public static readonly KnownHeader Cookie2 = new("Cookie2");
        public static readonly KnownHeader Date = new("Date", HttpHeaderType.General, DateHeaderParser.Parser);
        public static readonly KnownHeader ETag = new("ETag", HttpHeaderType.Response, GenericHeaderParser.SingleValueEntityTagParser);
        public static readonly KnownHeader Expect = new("Expect", HttpHeaderType.Request, GenericHeaderParser.MultipleValueNameValueWithParametersParser, new string[] { "100-continue" });
        public static readonly KnownHeader Expires = new("Expires", HttpHeaderType.Content, DateHeaderParser.Parser);
        public static readonly KnownHeader From = new("From", HttpHeaderType.Request, GenericHeaderParser.MailAddressParser);
        public static readonly KnownHeader Host = new("Host", HttpHeaderType.Request, GenericHeaderParser.HostParser);
        public static readonly KnownHeader IfMatch = new("If-Match", HttpHeaderType.Request, GenericHeaderParser.MultipleValueEntityTagParser);
        public static readonly KnownHeader IfModifiedSince = new("If-Modified-Since", HttpHeaderType.Request, DateHeaderParser.Parser);
        public static readonly KnownHeader IfNoneMatch = new("If-None-Match", HttpHeaderType.Request, GenericHeaderParser.MultipleValueEntityTagParser);
        public static readonly KnownHeader IfRange = new("If-Range", HttpHeaderType.Request, GenericHeaderParser.RangeConditionParser);
        public static readonly KnownHeader IfUnmodifiedSince = new("If-Unmodified-Since", HttpHeaderType.Request, DateHeaderParser.Parser);
        public static readonly KnownHeader KeepAlive = new("Keep-Alive");
        public static readonly KnownHeader LastModified = new("Last-Modified", HttpHeaderType.Content, DateHeaderParser.Parser);
        public static readonly KnownHeader Link = new("Link");
        public static readonly KnownHeader Location = new("Location", HttpHeaderType.Response, UriHeaderParser.RelativeOrAbsoluteUriParser);
        public static readonly KnownHeader MaxForwards = new("Max-Forwards", HttpHeaderType.Request, Int32NumberHeaderParser.Parser);
        public static readonly KnownHeader Origin = new("Origin");
        public static readonly KnownHeader P3P = new("P3P");
        public static readonly KnownHeader Pragma = new("Pragma", HttpHeaderType.General, GenericHeaderParser.MultipleValueNameValueParser);
        public static readonly KnownHeader ProxyAuthenticate = new("Proxy-Authenticate", HttpHeaderType.Response, GenericHeaderParser.MultipleValueAuthenticationParser);
        public static readonly KnownHeader ProxyAuthorization = new("Proxy-Authorization", HttpHeaderType.Request, GenericHeaderParser.SingleValueAuthenticationParser);
        public static readonly KnownHeader ProxyConnection = new("Proxy-Connection");
        public static readonly KnownHeader ProxySupport = new("Proxy-Support");
        public static readonly KnownHeader PublicKeyPins = new("Public-Key-Pins");
        public static readonly KnownHeader Range = new("Range", HttpHeaderType.Request, GenericHeaderParser.RangeParser);
        public static readonly KnownHeader Referer = new("Referer", HttpHeaderType.Request, UriHeaderParser.RelativeOrAbsoluteUriParser); // NB: The spelling-mistake "Referer" for "Referrer" must be matched.
        public static readonly KnownHeader RetryAfter = new("Retry-After", HttpHeaderType.Response, GenericHeaderParser.RetryConditionParser);
        public static readonly KnownHeader SecWebSocketAccept = new("Sec-WebSocket-Accept");
        public static readonly KnownHeader SecWebSocketExtensions = new("Sec-WebSocket-Extensions");
        public static readonly KnownHeader SecWebSocketKey = new("Sec-WebSocket-Key");
        public static readonly KnownHeader SecWebSocketProtocol = new("Sec-WebSocket-Protocol");
        public static readonly KnownHeader SecWebSocketVersion = new("Sec-WebSocket-Version");
        public static readonly KnownHeader Server = new("Server", HttpHeaderType.Response, ProductInfoHeaderParser.MultipleValueParser);
        public static readonly KnownHeader SetCookie = new("Set-Cookie");
        public static readonly KnownHeader SetCookie2 = new("Set-Cookie2");
        public static readonly KnownHeader StrictTransportSecurity = new("Strict-Transport-Security");
        public static readonly KnownHeader TE = new("TE", HttpHeaderType.Request, TransferCodingHeaderParser.MultipleValueWithQualityParser);
        public static readonly KnownHeader TSV = new("TSV");
        public static readonly KnownHeader Trailer = new("Trailer", HttpHeaderType.General, GenericHeaderParser.TokenListParser);
        public static readonly KnownHeader TransferEncoding = new("Transfer-Encoding", HttpHeaderType.General, TransferCodingHeaderParser.MultipleValueParser, new string[] { "chunked" });
        public static readonly KnownHeader Upgrade = new("Upgrade", HttpHeaderType.General, GenericHeaderParser.MultipleValueProductParser);
        public static readonly KnownHeader UpgradeInsecureRequests = new("Upgrade-Insecure-Requests");
        public static readonly KnownHeader UserAgent = new("User-Agent", HttpHeaderType.Request, ProductInfoHeaderParser.MultipleValueParser);
        public static readonly KnownHeader Vary = new("Vary", HttpHeaderType.Response, GenericHeaderParser.TokenListParser);
        public static readonly KnownHeader Via = new("Via", HttpHeaderType.General, GenericHeaderParser.MultipleValueViaParser);
        public static readonly KnownHeader WWWAuthenticate = new("WWW-Authenticate", HttpHeaderType.Response, GenericHeaderParser.MultipleValueAuthenticationParser);
        public static readonly KnownHeader Warning = new("Warning", HttpHeaderType.General, GenericHeaderParser.MultipleValueWarningParser);
        public static readonly KnownHeader XAspNetVersion = new("X-AspNet-Version");
        public static readonly KnownHeader XContentDuration = new("X-Content-Duration");
        public static readonly KnownHeader XContentTypeOptions = new("X-Content-Type-Options");
        public static readonly KnownHeader XFrameOptions = new("X-Frame-Options");
        public static readonly KnownHeader XMSEdgeRef = new("X-MSEdge-Ref");
        public static readonly KnownHeader XPoweredBy = new("X-Powered-By");
        public static readonly KnownHeader XRequestID = new("X-Request-ID");
        public static readonly KnownHeader XUACompatible = new("X-UA-Compatible");

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
        private static KnownHeader GetCandidate<T>(T key)
            where T : struct, IHeaderNameAccessor     // Enforce struct for performance
        {
            int length = key.Length;
            switch (length)
            {
                case 2:
                    return TE; // TE

                case 3:
                    switch (key[0])
                    {
                        case 'A': case 'a': return Age; // [A]ge
                        case 'P': case 'p': return P3P; // [P]3P
                        case 'T': case 't': return TSV; // [T]SV
                        case 'V': case 'v': return Via; // [V]ia
                    }
                    break;

                case 4:
                    switch (key[0])
                    {
                        case 'D': case 'd': return Date; // [D]ate
                        case 'E': case 'e': return ETag; // [E]Tag
                        case 'F': case 'f': return From; // [F]rom
                        case 'H': case 'h': return Host; // [H]ost
                        case 'L': case 'l': return Link; // [L]ink
                        case 'V': case 'v': return Vary; // [V]ary
                    }
                    break;

                case 5:
                    switch (key[0])
                    {
                        case 'A': case 'a': return Allow; // [A]llow
                        case 'R': case 'r': return Range; // [R]ange
                    }
                    break;

                case 6:
                    switch (key[0])
                    {
                        case 'A': case 'a': return Accept; // [A]ccept
                        case 'C': case 'c': return Cookie; // [C]ookie
                        case 'E': case 'e': return Expect; // [E]xpect
                        case 'O': case 'o': return Origin; // [O]rigin
                        case 'P': case 'p': return Pragma; // [P]ragma
                        case 'S': case 's': return Server; // [S]erver
                    }
                    break;

                case 7:
                    switch (key[0])
                    {
                        case 'A': case 'a': return AltSvc;  // [A]lt-Svc
                        case 'C': case 'c': return Cookie2; // [C]ookie2
                        case 'E': case 'e': return Expires; // [E]xpires
                        case 'R': case 'r': return Referer; // [R]eferer
                        case 'T': case 't': return Trailer; // [T]railer
                        case 'U': case 'u': return Upgrade; // [U]pgrade
                        case 'W': case 'w': return Warning; // [W]arning
                    }
                    break;

                case 8:
                    switch (key[3])
                    {
                        case 'M': case 'm': return IfMatch;  // If-[M]atch
                        case 'R': case 'r': return IfRange;  // If-[R]ange
                        case 'A': case 'a': return Location; // Loc[a]tion
                    }
                    break;

                case 10:
                    switch (key[0])
                    {
                        case 'C': case 'c': return Connection; // [C]onnection
                        case 'K': case 'k': return KeepAlive;  // [K]eep-Alive
                        case 'S': case 's': return SetCookie;  // [S]et-Cookie
                        case 'U': case 'u': return UserAgent;  // [U]ser-Agent
                    }
                    break;

                case 11:
                    switch (key[0])
                    {
                        case 'C': case 'c': return ContentMD5; // [C]ontent-MD5
                        case 'R': case 'r': return RetryAfter; // [R]etry-After
                        case 'S': case 's': return SetCookie2; // [S]et-Cookie2
                    }
                    break;

                case 12:
                    switch (key[2])
                    {
                        case 'C': case 'c': return AcceptPatch; // Ac[c]ept-Patch
                        case 'N': case 'n': return ContentType; // Co[n]tent-Type
                        case 'X': case 'x': return MaxForwards; // Ma[x]-Forwards
                        case 'M': case 'm': return XMSEdgeRef;  // X-[M]SEdge-Ref
                        case 'P': case 'p': return XPoweredBy;  // X-[P]owered-By
                        case 'R': case 'r': return XRequestID;  // X-[R]equest-ID
                    }
                    break;

                case 13:
                    switch (key[6])
                    {
                        case '-': return AcceptRanges;            // Accept[-]Ranges
                        case 'I': case 'i': return Authorization; // Author[i]zation
                        case 'C': case 'c': return CacheControl;  // Cache-[C]ontrol
                        case 'T': case 't': return ContentRange;  // Conten[t]-Range
                        case 'E': case 'e': return IfNoneMatch;   // If-Non[e]-Match
                        case 'O': case 'o': return LastModified;  // Last-M[o]dified
                        case 'S': case 's': return ProxySupport;  // Proxy-[S]upport
                    }
                    break;

                case 14:
                    switch (key[0])
                    {
                        case 'A': case 'a': return AcceptCharset; // [A]ccept-Charset
                        case 'C': case 'c': return ContentLength; // [C]ontent-Length
                    }
                    break;

                case 15:
                    switch (key[7])
                    {
                        case '-': return XFrameOptions;  // X-Frame[-]Options
                        case 'M': case 'm': return XUACompatible;  // X-UA-Co[m]patible
                        case 'E': case 'e': return AcceptEncoding; // Accept-[E]ncoding
                        case 'K': case 'k': return PublicKeyPins;  // Public-[K]ey-Pins
                        case 'L': case 'l': return AcceptLanguage; // Accept-[L]anguage
                    }
                    break;

                case 16:
                    switch (key[11])
                    {
                        case 'O': case 'o': return ContentEncoding; // Content-Enc[o]ding
                        case 'G': case 'g': return ContentLanguage; // Content-Lan[g]uage
                        case 'A': case 'a': return ContentLocation; // Content-Loc[a]tion
                        case 'C': case 'c': return ProxyConnection; // Proxy-Conne[c]tion
                        case 'I': case 'i': return WWWAuthenticate; // WWW-Authent[i]cate
                        case 'R': case 'r': return XAspNetVersion;  // X-AspNet-Ve[r]sion
                    }
                    break;

                case 17:
                    switch (key[0])
                    {
                        case 'I': case 'i': return IfModifiedSince;  // [I]f-Modified-Since
                        case 'S': case 's': return SecWebSocketKey;  // [S]ec-WebSocket-Key
                        case 'T': case 't': return TransferEncoding; // [T]ransfer-Encoding
                    }
                    break;

                case 18:
                    switch (key[0])
                    {
                        case 'P': case 'p': return ProxyAuthenticate; // [P]roxy-Authenticate
                        case 'X': case 'x': return XContentDuration;  // [X]-Content-Duration
                    }
                    break;

                case 19:
                    switch (key[0])
                    {
                        case 'C': case 'c': return ContentDisposition; // [C]ontent-Disposition
                        case 'I': case 'i': return IfUnmodifiedSince;  // [I]f-Unmodified-Since
                        case 'P': case 'p': return ProxyAuthorization; // [P]roxy-Authorization
                    }
                    break;

                case 20:
                    return SecWebSocketAccept; // Sec-WebSocket-Accept

                case 21:
                    return SecWebSocketVersion; // Sec-WebSocket-Version

                case 22:
                    switch (key[0])
                    {
                        case 'A': case 'a': return AccessControlMaxAge;  // [A]ccess-Control-Max-Age
                        case 'S': case 's': return SecWebSocketProtocol; // [S]ec-WebSocket-Protocol
                        case 'X': case 'x': return XContentTypeOptions;  // [X]-Content-Type-Options
                    }
                    break;

                case 23:
                    return ContentSecurityPolicy; // Content-Security-Policy

                case 24:
                    return SecWebSocketExtensions; // Sec-WebSocket-Extensions

                case 25:
                    switch (key[0])
                    {
                        case 'S': case 's': return StrictTransportSecurity; // [S]trict-Transport-Security
                        case 'U': case 'u': return UpgradeInsecureRequests; // [U]pgrade-Insecure-Requests
                    }
                    break;

                case 27:
                    return AccessControlAllowOrigin; // Access-Control-Allow-Origin

                case 28:
                    switch (key[21])
                    {
                        case 'H': case 'h': return AccessControlAllowHeaders; // Access-Control-Allow-[H]eaders
                        case 'M': case 'm': return AccessControlAllowMethods; // Access-Control-Allow-[M]ethods
                    }
                    break;

                case 29:
                    return AccessControlExposeHeaders; // Access-Control-Expose-Headers

                case 32:
                    return AccessControlAllowCredentials; // Access-Control-Allow-Credentials
            }

            return null;
        }

        internal static KnownHeader TryGetKnownHeader(string name)
        {
            KnownHeader candidate = GetCandidate(new StringAccessor(name));
            if (candidate != null && StringComparer.OrdinalIgnoreCase.Equals(name, candidate.Name))
            {
                return candidate;
            }

            return null;
        }

        internal static unsafe KnownHeader TryGetKnownHeader(ReadOnlySpan<byte> name)
        {
            fixed (byte* p = &MemoryMarshal.GetReference(name))
            {
                KnownHeader candidate = GetCandidate(new BytePtrAccessor(p, name.Length));
                if (candidate != null && ByteArrayHelpers.EqualsOrdinalAsciiIgnoreCase(candidate.Name, name))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}