using Nito.AsyncEx;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace HomeKit.Http
{
    internal sealed class HttpOperationOnStream
    {
        public HttpOperationOnStream(Stream underlyingStream,
                                     AsyncProducerConsumerQueue<HttpResponseMessage> eventQueue)
        {
            this.httpResponseParser = new HttpResponseParser(new NetworkReadStream(underlyingStream), eventQueue);
            this.underlyingStream = underlyingStream;
        }

        public async Task<HttpResponseMessage> Request(HttpRequestMessage request,
                                                       CancellationToken token)
        {
            // this lock prevents multiple requests in progress
            using var requestInProgress = await requestLock.LockAsync(token).ConfigureAwait(false);
            HttpResponseMessage? response = null;
            AsyncManualResetEvent waitForResult = new();
            httpResponseParser.AddHttpResponseCallback((result) =>
            {
                response = result;
                waitForResult.Set();
            });

            var httpSenderSerializer = new HttpRequestSerializer(request);
            var data = await httpSenderSerializer.ConvertToBytes().ConfigureAwait(false);

            var writeTransformCopy = this.writeTransform;
            if (writeTransformCopy != null)
            {
                var transFormedData = writeTransformCopy.Transform(data);
                await underlyingStream.WriteAsync(transFormedData, 0, transFormedData.Length, token).ConfigureAwait(false);
            }
            else
            {
                await underlyingStream.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
            }

            await underlyingStream.FlushAsync(token).ConfigureAwait(false);

            // Create timer task
            CancellationTokenSource timerToken = new();
            timerToken.CancelAfter(CalltimeoutMilliseconds);

            var cancellationTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(timerToken.Token, token);

            var waitTask = waitForResult.WaitAsync(cancellationTokenSource.Token);

            var finishedTask = Task.WhenAny(waitTask, readAndParseTask);

            // this allow to throw error if parsing fails.
            await finishedTask.ConfigureAwait(false);

            if (response == null)
            {
                throw new InvalidDataException("http response unexpected null");
            }

            response.RequestMessage = request;
            return response;
        }

        public async Task StartListening(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            token.Register(() => underlyingStream.Dispose());
            readAndParseTask = httpResponseParser.ReadAndParse(token);
            await readAndParseTask.ConfigureAwait(false);
        }

        public void UpdateTransforms(IReadTransform readTransform,
                                     IWriteTransform writeTransform)
        {
            Interlocked.Exchange(ref this.writeTransform, writeTransform);
            httpResponseParser.SetDataTransform(readTransform);
        }

        private sealed class NetworkReadStream : INetworkReadStream
        {
            private readonly Stream stream;

            public NetworkReadStream(Stream stream)
            {
                this.stream = stream;
            }

            public async Task<int> ReadAsync(byte[] buffer, int index, int v, CancellationToken cancellationToken)
            {
                return await stream.ReadAsync(buffer, index, v, cancellationToken);
            }
        }

        private const int CalltimeoutMilliseconds = 30 * 1000;
        private readonly HttpResponseParser httpResponseParser;
        private readonly AsyncLock requestLock = new();
        private readonly Stream underlyingStream;
        private Task? readAndParseTask;
        private volatile IWriteTransform? writeTransform;
    }
}