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

namespace HomeKitTest
{
    [TestClass]
    public class HttpResponseParserTest
    {
        public HttpResponseParserTest()
        {
            cancellationTokenSource.CancelAfter(30 * 1000);
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

            await TestResponse(data, body, expected);
        }

        [TestMethod]
        public async Task JsonData()
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

            await TestResponse(data, body, expected);
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

            await TestResponse(data, Array.Empty<byte>(), expected);
        }

        [TestMethod]
        public async Task TestChunkedResponse()
        {
            string data = "HTTP/1.1 200 OK\r\nContent-Type: application/hap+json\r\n" +
                          "Transfer-Encoding: chunked\r\n\r\n";

            string body = "5f\r\n{\"characteristics\":[{\"aid\":1,\"iid\":10,\"value\":35}," +
                           "{\"aid\":1,\"iid\":13,\"value\":36.0999984741211}]}\r\n" +
                           "0\r\n\r\n";

            var expected = new HttpResponseMessage()
            {
                Version = HttpVersion.Version11,
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"characteristics\":[{\"aid\":1,\"iid\":10,\"value\":35},{\"aid\":1,\"iid\":13,\"value\":36.0999984741211}]}", Encoding.UTF8),
            };

            expected.Headers.TransferEncodingChunked = true;
            expected.Content.Headers.ContentLength = null;
            expected.Content.Headers.ContentType = new MediaTypeHeaderValue("application/hap+json");

            await TestResponse(data, body, expected);
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

        private async Task<HttpResponseMessage> ReadAndParseResponse(MemoryStream stream,
                                        AsyncProducerConsumerQueue<HttpResponseMessage> eventQueue)
        {
            HttpResponseMessage httpResponseMessage = null;
            var parser = new HttpResponseParser(stream, eventQueue);

            var waitForResult = new AsyncManualResetEvent();
            parser.AddHttpResponseCallback((result) =>
            {
                httpResponseMessage = result;
                waitForResult.Set();
            });

            var _ = parser.ReadAndParse(cancellationTokenSource.Token);
            await waitForResult.WaitAsync(cancellationTokenSource.Token);

            return httpResponseMessage;
        }

        private async Task TestResponse(string serverData,
                                        string body,
                                        HttpResponseMessage expected)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            await TestResponse(serverData, bodyBytes, expected);
        }

        private async Task TestResponse(string serverData,
                                        byte[] bodyBytes,
                                        HttpResponseMessage expected)
        {
            var headerBytes = Encoding.UTF8.GetBytes(serverData);
            MemoryStream stream = new MemoryStream();
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);

            stream.Position = 0;

            var eventQueue = new AsyncProducerConsumerQueue<HttpResponseMessage>();
            var httpResponseMessage = await ReadAndParseResponse(stream, eventQueue);

            await CheckResponseSame(expected, httpResponseMessage);
            cancellationTokenSource.Cancel();
        }

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    }
}