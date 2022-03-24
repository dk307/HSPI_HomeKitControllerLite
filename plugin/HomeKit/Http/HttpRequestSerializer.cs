using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace HomeKit.Http
{
    internal sealed class HttpRequestSerializer
    {
        public HttpRequestSerializer(HttpRequestMessage request)
        {
            bool isNotSupported = (request.HasHeaders() && (request.Headers.ExpectContinue == true || request.Headers.TransferEncodingChunked == true)) ||
                                  ((request.Method != HttpMethod.Get) && (request.Method != HttpMethod.Post) && (request.Method != HttpMethod.Put)) ||
                                  (request.Version.Minor == 0 && request.Version.Major == 1);

            if (isNotSupported)
            {
                throw new NotSupportedException("Request not supported");
            }

            this.request = request;
        }

        public async Task<byte[]> ConvertToBytes()
        {
            var normalizedMethod = HttpMethodUtils.Normalize(request.Method);

            WriteStringAsync(normalizedMethod.Method);
            WriteByteAsync((byte)' ');

            // Write request line
            WriteStringAsync(request.RequestUri.GetComponents(UriComponents.PathAndQuery | UriComponents.Fragment, UriFormat.UriEscaped));

            WriteBytesAsync(s_spaceHttp11NewlineAsciiBytes);

            // Write request headers
            if (request.HasHeaders())
            {
                WriteHeadersAsync(request.Headers);
            }

            if (request.Content is null)
            {
                // Write out Content-Length: 0 header to indicate no body,
                // unless this is a method that never has a body.
                if (!ReferenceEquals(normalizedMethod, HttpMethod.Get))
                {
                    WriteBytesAsync(s_contentLength0NewlineAsciiBytes);
                }
            }
            else
            {
                // Write content headers
                WriteHeadersAsync(request.Content.Headers);
            }

            // Write special additional headers.  If a host isn't in the headers list, then a Host header
            // wasn't sent, so as it's required by HTTP 1.1 spec, send one based on the Request Uri.
            if (!request.HasHeaders() || request.Headers.Host == null)
            {
                WriteHostHeaderAsync(request.RequestUri);
            }

            // CRLF for end of headers.
            WriteTwoBytesAsync((byte)'\r', (byte)'\n');

            // Add the body if there is one.
            if (request.Content != null)
            {
                await request.Content.CopyToAsync(senderMemoryStream).ConfigureAwait(false);
            }
            return senderMemoryStream.ToArray();
        }

        private void WriteAsciiStringAsync(string s)
        {
            WriteStringAsync(s);
        }

        private void WriteByteAsync(byte b)
        {
            senderMemoryStream.WriteByte(b);
        }

        private void WriteBytesAsync(byte[] bytes)
        {
            senderMemoryStream.Write(bytes, 0, bytes.Length);
        }

        private void WriteDecimalInt32Async(int value)
        {
            WriteAsciiStringAsync(value.ToString());
        }

        private void WriteHeadersAsync(HttpHeaders headers)
        {
            foreach (var header in headers.GetHeaderDescriptorsAndValues())
            {
                if (header.Key.KnownHeader != null)
                {
                    WriteBytesAsync(header.Key.KnownHeader.AsciiBytesWithColonSpace);
                }
                else
                {
                    WriteAsciiStringAsync(header.Key.Name);
                    WriteTwoBytesAsync((byte)':', (byte)' ');
                }

                if (header.Value.Length > 0)
                {
                    WriteStringAsync(header.Value[0]);

                    // Some headers such as User-Agent and Server use space as a separator (see: ProductInfoHeaderParser)
                    if (header.Value.Length > 1)
                    {
                        var parser = header.Key.Parser;
                        var separator = HttpHeaderParser.DefaultSeparator;
                        if (parser != null && parser.SupportsMultipleValues)
                        {
                            separator = parser.Separator;
                        }

                        for (int i = 1; i < header.Value.Length; i++)
                        {
                            WriteAsciiStringAsync(separator);
                            WriteStringAsync(header.Value[i]);
                        }
                    }
                }

                WriteTwoBytesAsync((byte)'\r', (byte)'\n');
            }
        }

        private void WriteHostHeaderAsync(Uri uri)
        {
            WriteBytesAsync(KnownHeaders.Host.AsciiBytesWithColonSpace);

            if (uri.HostNameType == UriHostNameType.IPv6)
            {
                WriteByteAsync((byte)'[');
                WriteAsciiStringAsync(uri.IdnHost);
                WriteByteAsync((byte)']');
            }
            else
            {
                WriteAsciiStringAsync(uri.IdnHost);
            }

            if (!uri.IsDefaultPort)
            {
                WriteByteAsync((byte)':');
                WriteDecimalInt32Async(uri.Port);
            }

            WriteTwoBytesAsync((byte)'\r', (byte)'\n');
        }

        private void WriteStringAsync(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c & 0xFF80) != 0)
                {
                    throw new HttpRequestException("net_http_request_invalid_char_encoding");
                }
                WriteByteAsync((byte)c);
            }
        }

        private void WriteTwoBytesAsync(byte b1, byte b2)
        {
            WriteByteAsync(b1);
            WriteByteAsync(b2);
        }

        private static readonly byte[] s_contentLength0NewlineAsciiBytes = Encoding.ASCII.GetBytes("Content-Length: 0\r\n");
        private static readonly byte[] s_spaceHttp11NewlineAsciiBytes = Encoding.ASCII.GetBytes(" HTTP/1.1\r\n");
        private readonly HttpRequestMessage request;
        private readonly MemoryStream senderMemoryStream = new();
    }
}