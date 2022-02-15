using HomeKit.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nito.AsyncEx;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class HttpResponseParserTest
    {
        public HttpResponseParserTest()
        {
            cancellationTokenSource.CancelAfter(30 * 1000);
        }

        [DataTestMethod]
        [DataRow(10)]
        [DataRow(4096)]
        public async Task EventBeforeContent(int maxResponseChunkSize)
        {
            string data = "EVENT/1.0 200 OK\r\nContent-Type: application/hap+json\r\nTransfer-Encoding: chunked\r\n\r\n" +
                          "5f\r\n{\"characteristics\":[{\"aid\":1,\"iid\":10,\"value\":35}," +
                          "{\"aid\":1,\"iid\":13,\"value\":36.0999984741211}]}\r\n" +
                          "0\r\n\r\n" +
                          "HTTP/1.1 204 No content\r\n\r\n";

            var expected = new HttpResponseMessage()
            {
                Version = HttpVersion.Version11,
                StatusCode = HttpStatusCode.NoContent,
            };

            var queue = await TestResponse(data, Array.Empty<byte>(), expected, maxResponseChunkSize);
            await queue.DequeueAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task InternalServerErrorWithStatusBody()
        {
            string data = "HTTP/1.1 500 Internal Server Error\r\n" +
                          "Content-Type: application/hap+json\r\n" +
                          "Content-Length: 17\r\n\r\n";

            string body = "{\"status\":-70402}";

            var expected = new HttpResponseMessage()
            {
                Version = HttpVersion.Version11,
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent(body, Encoding.UTF8),
            };

            expected.Content.Headers.ContentLength = 17;
            expected.Content.Headers.ContentType = new MediaTypeHeaderValue("application/hap+json");

            await TestResponse(data, body, expected, int.MaxValue);
        }

        [DataTestMethod]
        [DataRow(10)]
        [DataRow(4096)]
        public async Task JsonData(int maxResponseChunkSize)
        {
            string data = "HTTP/1.1 200 OK\r\n" +
                          "Content-Type: application/hap+json\r\n" +
                          "Content-Length: 95\r\n" +
                          "Connection: keep-alive\r\n" +
                          "\r\n";

            string body = "{\"characteristics\":[{\"aid\":1,\"iid\":10,\"value\":35}," +
                          "{\"aid\":1,\"iid\":13,\"value\":36.0999984741211}]}";

            var expected = new HttpResponseMessage()
            {
                Version = HttpVersion.Version11,
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(body, Encoding.UTF8)
            };

            expected.Headers.Connection.Add("keep-alive");

            expected.Content.Headers.ContentLength = 95;
            expected.Content.Headers.ContentType = new MediaTypeHeaderValue("application/hap+json");

            await TestResponse(data, body, expected, maxResponseChunkSize);
        }

        [TestMethod]
        public async Task NoContent()
        {
            string data = "HTTP/1.1 204 No content\r\n\r\n";

            var expected = new HttpResponseMessage()
            {
                Version = HttpVersion.Version11,
                StatusCode = HttpStatusCode.NoContent,
            };

            await TestResponse(data, Array.Empty<byte>(), expected, int.MaxValue);
        }

        [TestMethod]
        public async Task WrongHttpVersionResponse()
        {
            string data = "HTTP/1.0 204 No content\r\n\r\n";

            var expected = new HttpResponseMessage()
            {
                Version = HttpVersion.Version11,
                StatusCode = HttpStatusCode.NoContent,
            };

            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => TestResponse(data, Array.Empty<byte>(), expected, int.MaxValue));
        }

        [DataTestMethod]
        [DataRow("HTTP/1.1 200 OK\r\nContent-Type: application/hap+json\r\nTransfer-Encoding: chunked\r\n\r\n",
                 "5f\r\n{\"characteristics\":[{\"aid\":1,\"iid\":10,\"value\":35}," +
                           "{\"aid\":1,\"iid\":13,\"value\":36.0999984741211}]}\r\n" +
                           "0\r\n\r\n",
                "{\"characteristics\":[{\"aid\":1,\"iid\":10,\"value\":35},{\"aid\":1,\"iid\":13,\"value\":36.0999984741211}]}",
                 int.MaxValue,
                DisplayName = "Single chuncked body")]
        [DataRow("HTTP/1.1 200 OK\r\nContent-Type: application/hap+json\r\nTransfer-Encoding: chunked\r\n\r\n",
                 "2\r\n{\"\r\n" +
                 "1\r\nc\r\n" +
                 "5c\r\nharacteristics\":[{\"aid\":1,\"iid\":10,\"value\":35}," +
                           "{\"aid\":1,\"iid\":13,\"value\":36.0999984741211}]}\r\n" +
                           "0\r\n\r\n",
                "{\"characteristics\":[{\"aid\":1,\"iid\":10,\"value\":35},{\"aid\":1,\"iid\":13,\"value\":36.0999984741211}]}",
                10,
                DisplayName = "Multiple chuncked body")]
        public async Task TestChunkedResponse(string data, string body, string expectedBody, int maxResponseChunkSize)
        {
            var expected = new HttpResponseMessage()
            {
                Version = HttpVersion.Version11,
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(expectedBody, Encoding.UTF8),
            };

            expected.Headers.TransferEncodingChunked = true;
            expected.Content.Headers.ContentLength = null;
            expected.Content.Headers.ContentType = new MediaTypeHeaderValue("application/hap+json");

            await TestResponse(data, body, expected, maxResponseChunkSize);
        }

        private static async Task CheckResponseSame(HttpResponseMessage expected,
                                                    HttpResponseMessage httpResponseMessage)
        {
            Assert.AreEqual(expected.Version, httpResponseMessage.Version);
            Assert.AreEqual(expected.StatusCode, httpResponseMessage.StatusCode);
            Assert.AreEqual(expected.Headers.ToString(), httpResponseMessage.Headers.ToString());

            if (expected.Content != null)
            {
                Assert.AreEqual(expected.Content.Headers.ContentType, httpResponseMessage.Content.Headers.ContentType);
                Assert.AreEqual(expected.Content.Headers.ContentLength, httpResponseMessage.Content.Headers.ContentLength);
                Assert.AreEqual(expected.Content.Headers.ToString(), httpResponseMessage.Content.Headers.ToString());
                byte[] expectedContent = await expected.Content.ReadAsByteArrayAsync();
                byte[] actualContent = await httpResponseMessage.Content.ReadAsByteArrayAsync();
                CollectionAssert.AreEqual(expectedContent, actualContent);
            }
            else
            {
                Assert.IsNull(expected.Content);
            }
        }

        private async Task TestResponse(string serverData,
                                        string body,
                                        HttpResponseMessage expected,
                                        int maxResponseChunkSize)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            await TestResponse(serverData, bodyBytes, expected, maxResponseChunkSize);
        }

        private async Task<AsyncProducerConsumerQueue<HttpResponseMessage>>
            TestResponse(string serverData, 
                         byte[] bodyBytes, 
                         HttpResponseMessage expected,
                         int maxResponseChunkSize)
        {
            var headerBytes = Encoding.UTF8.GetBytes(serverData);
            MemoryStream stream = new();
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);

            stream.Position = 0;

            var eventQueue = new AsyncProducerConsumerQueue<HttpResponseMessage>();
            HttpResponseMessage httpResponseMessage = null;

            MockNetworkReadStream readStream = new(stream, maxResponseChunkSize);
            var parser = new HttpResponseParser(readStream, eventQueue);

            var waitForResult = new AsyncManualResetEvent();
            parser.AddHttpResponseCallback((result) =>
            {
                httpResponseMessage = result;
                waitForResult.Set();
            });

            var readTask = parser.ReadAndParse(cancellationTokenSource.Token);
            var waitTask = waitForResult.WaitAsync(cancellationTokenSource.Token);

            var finishedTask = await Task.WhenAny(readTask, waitTask).ConfigureAwait(false);

            await finishedTask.ConfigureAwait(false);
            await waitTask.ConfigureAwait(false);

            await CheckResponseSame(expected, httpResponseMessage);
            cancellationTokenSource.Cancel();
            return eventQueue;
        }


        private readonly CancellationTokenSource cancellationTokenSource = new();
    }

}