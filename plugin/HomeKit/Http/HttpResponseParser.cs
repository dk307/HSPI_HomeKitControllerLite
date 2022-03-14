using Microsoft.Toolkit.HighPerformance;
using Nito.AsyncEx;
using Nito.Collections;
using System;
using System.Buffers.Text;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace HomeKit.Http
{
    internal interface INetworkReadStream
    {
        Task<int> ReadAsync(byte[] buffer, int index, int v, CancellationToken cancellationToken);
    }

    internal sealed class HttpResponseParser
    {
        public HttpResponseParser(INetworkReadStream stream,
                                  AsyncProducerConsumerQueue<HttpResponseMessage> eventQueue)
        {
            this.stream = stream;
            this.eventQueue = eventQueue;
            this.readBuffer = new ByteBufferWithIndex(InitialReadBufferSize);
            this.rawReadBuffer = new ByteBufferWithIndex(InitialReadBufferSize);
        }

        private enum ParsingState : byte
        {
            ExpectChunkHeader,
            ExpectChunkData,
            ExpectChunkTerminator,
            ConsumeTrailers,
            Done
        }

        private int ReadLength => readBuffer.Length;

        public void AddHttpResponseCallback(Action<HttpResponseMessage> callback)
        {
            httpResponseCallbacks.AddToBack(callback);
        }

        public async Task ReadAndParse(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                //Wait till data is available
                if (readBuffer.IsEmpty)
                {
                    await ReadAndTransform(cancellationToken).ConfigureAwait(false);

                    if (readBuffer.IsEmpty)
                    {
                        continue;
                    }
                }

                Debug.Assert(readOffset == 0);

                var response = new HttpResponseMessage()
                {
                    Content = new HttpConnectionResponseContent(),
                };

                var line = await ReadNextResponseHeaderLineAsync(cancellationToken).ConfigureAwait(false);
                bool isEvent = ParseStatusLine(line.Span, response);

                await ParseHeaders(response, cancellationToken).ConfigureAwait(false);

                await GetResponseContent(response, cancellationToken).ConfigureAwait(false);

                readBuffer.RemoveFromFront(readOffset);
                readOffset = 0;

                if (isEvent)
                {
                    await eventQueue.EnqueueAsync(response, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var result = httpResponseCallbacks.RemoveFromFront();
                    result(response); //callback
                }
            }
        }

        public void SetDataTransform(IReadTransform dataTransform)
        {
            Interlocked.Exchange(ref readTransform, dataTransform);
        }

        private static bool IsDigit(byte c) => (uint)(c - '0') <= '9' - '0';

        private static void ParseHeaderNameValue(ReadOnlySpan<byte> line,
                                                 HttpResponseMessage response)
        {
            Debug.Assert(line.Length > 0);

            int pos = 0;
            while (line[pos] != (byte)':' && line[pos] != (byte)' ')
            {
                pos++;
                if (pos == line.Length)
                {
                    // Invalid header line that doesn't contain ':'.
                    ThrowInvalidHttpResponse();
                }
            }

            if (pos == 0)
            {
                // Invalid empty header name.
                ThrowInvalidHttpResponse();
            }

            if (!HeaderDescriptor.TryGet(line.Slice(0, pos), out HeaderDescriptor descriptor))
            {
                // Invalid header name
                ThrowInvalidHttpResponse();
            }

            // Eat any trailing whitespace
            while (line[pos] == (byte)' ')
            {
                pos++;
                if (pos == line.Length)
                {
                    // Invalid header line that doesn't contain ':'.
                    ThrowInvalidHttpResponse();
                }
            }

            if (line[pos++] != ':')
            {
                // Invalid header line that doesn't contain ':'.
                ThrowInvalidHttpResponse();
            }

            // Skip whitespace after colon
            while (pos < line.Length && (line[pos] == (byte)' ' || line[pos] == (byte)'\t'))
            {
                pos++;
            }

            string headerValue = descriptor.GetHeaderValue(line.Slice(pos));

            // if the header can't be added, we silently drop it.
            if (descriptor.HeaderType == HttpHeaderType.Content)
            {
                response.Content.Headers.TryAddWithoutValidation(descriptor.Name, headerValue);
            }
            else
            {
                // Request headers returned on the response must be treated as custom headers
                response.Headers.TryAddWithoutValidation(descriptor.HeaderType == HttpHeaderType.Request ? descriptor.AsCustomHeader().Name : descriptor.Name, headerValue);
            }
        }

        private static bool ParseStatusLine(ReadOnlySpan<byte> line,
                                            HttpResponseMessage response)
        {
            bool isEvent = false;
            int indexAfterProtocol = 0;
            if (StartsWith(line, Http11Bytes))
            {
                response.Version = HttpVersion.Version11;
                indexAfterProtocol = Http11Bytes.Length + 1;
            }
            else if (StartsWith(line, Event10Bytes))
            {
                response.Version = HttpVersion.Version10;
                indexAfterProtocol = Event10Bytes.Length + 1;
                isEvent = true;
            }
            else
            {
                ThrowInvalidHttpResponse();
            }

            // Set the status code
            byte status1 = line[indexAfterProtocol],
                 status2 = line[indexAfterProtocol + 1],
                 status3 = line[indexAfterProtocol + 2];
            if (!IsDigit(status1) || !IsDigit(status2) || !IsDigit(status3))
            {
                ThrowInvalidHttpResponse();
            }

            response.StatusCode = ((HttpStatusCode)(100 * (status1 - '0') + 10 * (status2 - '0') + (status3 - '0')));
            return isEvent;
        }

        private static bool StartsWith(ReadOnlySpan<byte> source, byte[] data)
        {
            if (source.Length < data.Length)
            {
                return false;
            }

            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] != source[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static void ThrowInvalidHttpResponse() => throw new HttpRequestException("net_http_invalid_response");

        private static void ValidateChunkExtension(ReadOnlySpan<byte> lineAfterChunkSize)
        {
            // Until we see the ';' denoting the extension, the line after the chunk size
            // must contain only tabs and spaces.  After the ';', anything goes.
            for (int i = 0; i < lineAfterChunkSize.Length; i++)
            {
                byte c = lineAfterChunkSize[i];
                if (c == ';')
                {
                    break;
                }
                else if (c != ' ' && c != '\t') // not called out in the RFC, but WinHTTP allows it
                {
                    throw new IOException("net_http_invalid_response_chunk_extension_invalid");
                }
            }
        }

        private async ValueTask CopyChunkedBodyToAsync(HttpResponseMessage response, Stream destination, CancellationToken cancellationToken)
        {
            ulong chunkBytesRemaining = 0;
            ParsingState parsingState = ParsingState.ExpectChunkHeader;

            while (true)
            {
                while (true)
                {
                    if (ReadChunkFromConnectionBuffer(response, ref parsingState,
                                                      ref chunkBytesRemaining) is not ReadOnlyMemory<byte> bytesRead || bytesRead.Length == 0)
                    {
                        break;
                    }
                    await destination.WriteAsync(bytesRead, cancellationToken).ConfigureAwait(false);
                }

                if (parsingState == ParsingState.Done)
                {
                    // Fully consumed the response.
                    return;
                }

                await FillAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private async ValueTask CopyFromBufferAsync(Stream destination,
                                                    int count,
                                                    CancellationToken cancellationToken)
        {
            await destination.WriteAsync(readBuffer.AsMemory().Slice(readOffset, count),
                                         cancellationToken).ConfigureAwait(false);
            readOffset += count;
        }

        private async ValueTask CopyToExactLengthAsync(Stream destination,
                                                       ulong length,
                                                       CancellationToken cancellationToken)
        {
            int remaining = ReadLength - readOffset;
            if (remaining > 0)
            {
                if ((ulong)remaining > length)
                {
                    remaining = (int)length;
                }
                await CopyFromBufferAsync(destination, remaining, cancellationToken).ConfigureAwait(false);

                length -= (ulong)remaining;
                if (length == 0)
                {
                    return;
                }
            }

            while (true)
            {
                await FillAsync(cancellationToken).ConfigureAwait(false);

                remaining = (ulong)ReadLength < length ? ReadLength : (int)length;
                await CopyFromBufferAsync(destination, remaining, cancellationToken).ConfigureAwait(false);

                length -= (ulong)remaining;
                if (length == 0)
                {
                    return;
                }
            }
        }

        private async ValueTask FillAsync(CancellationToken cancellationToken)
        {
            int remaining = ReadLength - readOffset;
            Debug.Assert(remaining >= 0);

            if (readOffset > 0)
            {
                // There's some data in the buffer but it's not at the beginning.  Shift it
                // down to make room for more.
                readBuffer.RemoveFromFront(readOffset);
                readOffset = 0;
            }

            do
            {
                await ReadAndTransform(cancellationToken).ConfigureAwait(false);
            } while (readBuffer.IsEmpty);
        }

        private async ValueTask GetResponseContent(HttpResponseMessage response,
                                                   CancellationToken cancellationToken)
        {
            // we move the response content to memory as we expect it to be small
            MemoryStream responseStream = new();
            if (response.StatusCode == HttpStatusCode.NoContent ||
                response.StatusCode == HttpStatusCode.NotModified)
            {
                //done
            }
            else if (response.Content.Headers.ContentLength != null)
            {
                long contentLength = response.Content.Headers.ContentLength.GetValueOrDefault();
                if (contentLength <= 0)
                {
                    // nothing to copy
                }
                else
                {
                    await CopyToExactLengthAsync(responseStream,
                                                 (ulong)contentLength,
                                                 cancellationToken).ConfigureAwait(false);
                }
            }
            else if (response.Headers.TransferEncodingChunked == true)
            {
                await CopyChunkedBodyToAsync(response, responseStream,
                              cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Not sure how much data to copy
                throw new NotImplementedException();
            }

            var httpConnectionResponseContent = (HttpConnectionResponseContent)response.Content;
            responseStream.Position = 0;
            httpConnectionResponseContent.SetData(responseStream, cancellationToken);
        }

        private async ValueTask ParseHeaders(HttpResponseMessage response,
                                             CancellationToken cancellationToken)
        {
            // Parse the response headers. Logic after this point depends on being able to examine headers in the response object.
            while (true)
            {
                var line = await ReadNextResponseHeaderLineAsync(cancellationToken).ConfigureAwait(false);
                if (line.IsEmpty)
                {
                    break;
                }
                ParseHeaderNameValue(line.Span, response);
            }
        }

        private async ValueTask ReadAndTransform(CancellationToken cancellationToken)
        {
            await rawReadBuffer.ReadFromStream(stream,
                                               cancellationToken).ConfigureAwait(false);

            if (rawReadBuffer.IsEmpty)
            {
                // EOF reached
                throw new IOException("net_http_invalid_response");
            }

            var transform = this.readTransform;
            if (transform != null)
            {
                transform.Transform(rawReadBuffer, readBuffer);
            }
            else
            {
                byte[] data = rawReadBuffer.RemoveFromFront(rawReadBuffer.Length);
                readBuffer.AddToBack(data);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S907:\"goto\" statement should not be used", Justification = "<Pending>")]
        private ReadOnlyMemory<byte>? ReadChunkFromConnectionBuffer(HttpResponseMessage response,
                                                                    ref ParsingState parsingState,
                                                                    ref ulong chunkBytesRemaining)
        {
            ReadOnlySpan<byte> currentLine;
            switch (parsingState)
            {
                case ParsingState.ExpectChunkHeader:

                    // Read the chunk header line.
                    if (!TryReadNextLine(out currentLine))
                    {
                        // Could not get a whole line, so we can't parse the chunk header.
                        return default;
                    }

                    // Parse the hex value from it.
                    if (!Utf8Parser.TryParse(currentLine, out ulong chunkSize, out int bytesConsumed, 'X'))
                    {
                        throw new IOException("net_http_invalid_response_chunk_header_invalid");
                    }
                    chunkBytesRemaining = chunkSize;

                    // If there's a chunk extension after the chunk size, validate it.
                    if (bytesConsumed != currentLine.Length)
                    {
                        ValidateChunkExtension(currentLine.Slice(bytesConsumed));
                    }

                    // Proceed to handle the chunk.  If there's data in it, go read it.
                    // Otherwise, finish handling the response.
                    if (chunkSize > 0)
                    {
                        parsingState = ParsingState.ExpectChunkData;
                        goto case ParsingState.ExpectChunkData;
                    }
                    else
                    {
                        parsingState = ParsingState.ConsumeTrailers;
                        goto case ParsingState.ConsumeTrailers;
                    }

                case ParsingState.ExpectChunkData:
                    Debug.Assert(chunkBytesRemaining > 0);

                    ReadOnlyMemory<byte> connectionBuffer = readBuffer.AsMemory(readOffset, ReadLength - readOffset);
                    if (connectionBuffer.Length == 0)
                    {
                        return default;
                    }

                    int bytesToConsume = Math.Min(int.MaxValue, (int)Math.Min((ulong)connectionBuffer.Length, chunkBytesRemaining));
                    Debug.Assert(bytesToConsume > 0);

                    readOffset += bytesToConsume;
                    chunkBytesRemaining -= (ulong)bytesToConsume;
                    if (chunkBytesRemaining == 0)
                    {
                        parsingState = ParsingState.ExpectChunkTerminator;
                    }

                    return connectionBuffer.Slice(0, bytesToConsume);

                case ParsingState.ExpectChunkTerminator:

                    if (!TryReadNextLine(out currentLine))
                    {
                        return default;
                    }

                    if (currentLine.Length != 0)
                    {
                        throw new HttpRequestException("SR.net_http_invalid_response_chunk_terminator_invalid");
                    }

                    parsingState = ParsingState.ExpectChunkHeader;
                    goto case ParsingState.ExpectChunkHeader;

                case ParsingState.ConsumeTrailers:
                    Debug.Assert(chunkBytesRemaining == 0, $"Expected {nameof(chunkBytesRemaining)} == 0, got {chunkBytesRemaining}");

                    while (true)
                    {
                        if (!TryReadNextLine(out currentLine))
                        {
                            break;
                        }

                        if (currentLine.IsEmpty)
                        {
                            parsingState = ParsingState.Done;
                            break;
                        }
                        // Parse the trailer.
                        else
                        {
                            // Make sure that we don't inadvertently consume trailing headers
                            // while draining a connection that's being returned back to the pool.
                            ParseHeaderNameValue(currentLine, response);
                        }
                    }

                    return default;

                default:
                    Debug.Fail($"Unexpected state: {parsingState}");
                    return default;
            }
        }

        private async ValueTask<ReadOnlyMemory<byte>> ReadNextResponseHeaderLineAsync(CancellationToken token)
        {
            int previouslyScannedBytes = 0;
            while (true)
            {
                int scanOffset = readOffset + previouslyScannedBytes;
                int lfIndex = readBuffer.AsSpan().Slice(scanOffset).IndexOf((byte)'\n');
                if (lfIndex >= 0)
                {
                    lfIndex += scanOffset;
                    int startIndex = readOffset;
                    int length = lfIndex - startIndex;
                    if (lfIndex > 0 && readBuffer.AsSpan()[lfIndex - 1] == '\r')
                    {
                        length--;
                    }

                    // Advance read position past the LF
                    readOffset = lfIndex + 1;

                    return readBuffer.AsMemory().Slice(startIndex, length);
                }

                // Couldn't find LF.  Read more. Note this may cause _readOffset to change.
                previouslyScannedBytes = ReadLength - readOffset;
                await FillAsync(token).ConfigureAwait(false);
            }
        }

        private bool TryReadNextLine(out ReadOnlySpan<byte> line)
        {
            var buffer = readBuffer.AsSpan(readOffset, readBuffer.Length - readOffset);
            int length = buffer.IndexOf((byte)'\n');
            if (length < 0)
            {
                line = default;
                return false;
            }

            int bytesConsumed = length + 1;
            readOffset += bytesConsumed;

            line = buffer.Slice(0, length > 0 && buffer[length - 1] == '\r' ? length - 1 : length);
            return true;
        }

        private sealed class HttpConnectionResponseContent : HttpContent
        {
            public void SetData(Stream stream, CancellationToken cancellationToken)
            {
                Debug.Assert(!consumedStream);
                this.stream = stream;
                this.cancellationToken = cancellationToken;
            }

            internal async Task SerializeToStreamAsyncInternal(Stream stream, CancellationToken cancellationToken)
            {
                Debug.Assert(stream != null);

                using var contentStream = ConsumeStream();
                const int BufferSize = 8192;
                await contentStream.CopyToAsync(stream, BufferSize, cancellationToken).ConfigureAwait(false);
            }

            protected override sealed Task<Stream> CreateContentReadStreamAsync() =>
                Task.FromResult<Stream>(ConsumeStream());

            protected override sealed void Dispose(bool disposing)
            {
                if (disposing)
                {
                    stream?.Dispose();
                }

                base.Dispose(disposing);
            }

            protected override sealed Task SerializeToStreamAsync(Stream stream, TransportContext context) =>
                SerializeToStreamAsyncInternal(stream, cancellationToken);

            protected override sealed bool TryComputeLength(out long length)
            {
                length = 0;
                return false;
            }

            private Stream ConsumeStream()
            {
                if (consumedStream || stream is null)
                {
                    throw new InvalidOperationException("net_http_content_stream_already_read");
                }
                consumedStream = true;

                return stream;
            }

            private CancellationToken cancellationToken;
            private bool consumedStream;
            private Stream? stream;
        }

        private static readonly byte[] Event10Bytes = Encoding.ASCII.GetBytes("EVENT/1.0");
        private static readonly byte[] Http11Bytes = Encoding.ASCII.GetBytes("HTTP/1.1");
        private readonly Deque<Action<HttpResponseMessage>> httpResponseCallbacks = new();
        private readonly int InitialReadBufferSize = 4096;
        private readonly ByteBufferWithIndex rawReadBuffer;
        private readonly ByteBufferWithIndex readBuffer;
        private readonly INetworkReadStream stream;
        private readonly AsyncProducerConsumerQueue<HttpResponseMessage> eventQueue;
        private int readOffset = 0;
        private volatile IReadTransform? readTransform;
    }
}