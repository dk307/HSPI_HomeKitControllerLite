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
            this.httpResponseParser = new HttpResponseParser(underlyingStream, eventQueue);
            this.underlyingStream = underlyingStream;
        }

        public async Task<HttpResponseMessage> Request(HttpRequestMessage request,
                                                       CancellationToken token)
        {
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
            timerToken.CancelAfter(30 * 1000);

            var cancellationTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(timerToken.Token, token);

            await waitForResult.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            response.RequestMessage = request;
            return response;
        }

        public async Task StartListening(CancellationToken token)
        {
            await httpResponseParser.ReadAndParse(token).ConfigureAwait(false);
        }

        public void UpdateTransforms(IReadTransform readTransform,
                                     IWriteTransform writeTransform)
        {
            Interlocked.Exchange(ref this.writeTransform, writeTransform);
            httpResponseParser.SetDataTransform(readTransform);
        }

        private readonly HttpResponseParser httpResponseParser;
        private readonly Stream underlyingStream;
        private volatile IWriteTransform? writeTransform;
    }
}