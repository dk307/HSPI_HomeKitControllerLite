using HomeKit.Exceptions;
using HomeKit.Http;
using HomeKit.Model;
using Newtonsoft.Json;
using Nito.AsyncEx;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace HomeKit
{
    internal abstract class Connection : IDisposable
    {
        protected Connection(Device deviceInformation)
        {
            this.homeKitDeviceInformation = deviceInformation;
        }

        public IPEndPoint Address => homeKitDeviceInformation.Address;

        public virtual bool Connected
        {
            get
            {
                try
                {
                    return client?.Connected ?? false;
                }
                catch (Exception)
                {
                    // Connected check throws
                    return true;
                }
            }
        }

        public DeviceFeature DeviceFeature => homeKitDeviceInformation.Feature;
        public Device DeviceInformation => homeKitDeviceInformation;
        public string DisplayName => homeKitDeviceInformation.DisplayName;

        protected AsyncProducerConsumerQueue<HttpResponseMessage> EventQueue => eventQueue;
        protected NetworkStream UnderLyingStream => client?.GetStream() ?? throw new InvalidOperationException("Client not connected");

        public virtual async Task<Task> ConnectAndListen(CancellationToken token)
        {
            client = new TcpClient()
            {
                NoDelay = true,
                LingerState = new LingerOption(false, 0),
            };

            Log.Information("Connecting to {Name} at {EndPoint}", DisplayName, Address);
            await client.ConnectAsync(Address.Address, Address.Port).ConfigureAwait(false);

            Log.Information("Connected to {EndPoint}", Address);

            HttpOperationOnStream value = new(UnderLyingStream, eventQueue);
            return StartListening(value, token);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async Task<R?> HandleJsonRequest<T, R>(HttpMethod httpMethod,
                          T? value,
                          string target,
                          string contentType = JsonContentType,
                          CancellationToken cancellationToken = default) where R : class
        {
            byte[]? bytesContent = null;
            if (value != null)
            {
                JsonSerializerSettings jsonSerializerSettings = new()
                {
                    Formatting = Formatting.None
                };
                var content = JsonConvert.SerializeObject(value, typeof(T), jsonSerializerSettings);
                bytesContent = Encoding.UTF8.GetBytes(content);
            }

            var response = await Request(httpMethod, target,
                                         bytesContent, contentType, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return null;
            }

            string? mediaType = response.Content?.Headers?.ContentType?.MediaType;
            if (!response.IsSuccessStatusCode)
            {
                if (mediaType == JsonContentType)
                {
                    await TryParseHapStatus(target, response).ConfigureAwait(false);
                }

                Log.Error("Unexpected {StatusCode} response for {target} for {EndPoint}", response.StatusCode, target, Address);
                response.EnsureSuccessStatusCode();
            }

            if (mediaType != JsonContentType)
            {
                Log.Error("Unexpected {mediaType} response for {target} for {EndPoint}", mediaType, target, Address);
                throw new HttpRequestException("Unexpected response Type for request " + mediaType);
            }

            if (response.Content is null)
            {
                Log.Error("Unexpected No Body response for {target} for {EndPoint}", mediaType, target, Address);
                throw new HttpRequestException("Unexpected no body response for request " + mediaType);
            }

            var responseData = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<R>(responseData);
        }

        public async Task<IEnumerable<TlvValue>> PostTlv(IEnumerable<TlvValue> tlvList,
                                  string target,
                                  string contentType = TlvContentType,
                                  CancellationToken cancellationToken = default)
        {
            var content = Tlv8.Encode(tlvList);

            var response = await Request(HttpMethod.Post, target,
                          content, contentType, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("Unexpected {StatusCode} response for {target} for {EndPoint}", response.StatusCode, target, Address);
                response.EnsureSuccessStatusCode();
            }

            string mediaType = response.Content.Headers.ContentType.MediaType;
            if (mediaType != TlvContentType)
            {
                Log.Error("Unexpected {mediaType} response for {target} for {EndPoint}", mediaType, target, Address);
                throw new HttpRequestException("Unexpected response Type for request " + mediaType);
            }

            var responseData = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            responseData.Position = 0;
            return Tlv8.Decode(responseData);
        }

        public async Task<HttpResponseMessage> Request(HttpMethod httpMethod,
                                  string target,
                                  byte[]? content = null,
                                  string? contentType = null,
                                  CancellationToken token = default)
        {
            var builder = new UriBuilder
            {
                Port = Address.Port,
                Path = target,
                Host = Address.Address.ToString()
            };

            HttpRequestMessage request = new(httpMethod, builder.Uri);
            request.Headers.ExpectContinue = false;

            if (content != null)
            {
                request.Content = new ByteArrayContent(content);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                request.Content.Headers.ContentLength = content.Length;
            }

            return await Request(request, token).ConfigureAwait(false);
        }

        protected void Disconnect()
        {
            this.client?.Close();
        }

        protected async Task StartListening(HttpOperationOnStream value,
                                                      CancellationToken token)
        {
            httpOperationOnStream = value;
            await httpOperationOnStream.StartListening(token).ConfigureAwait(false);
        }

        protected void UpdateTransforms(IReadTransform readTransform, IWriteTransform writeTransform)
        {
            if (httpOperationOnStream is null)
            {
                throw new InvalidOperationException();
            }
            this.httpOperationOnStream.UpdateTransforms(readTransform, writeTransform);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                client?.Dispose();
            }
        }

        [MemberNotNull(nameof(client))]
        [MemberNotNull(nameof(httpOperationOnStream))]
        private void CheckConnectionValid()
        {
            if (client == null || httpOperationOnStream == null)
            {
                throw new AccessoryDisconnectedException("Connection never made");
            }
        }

        private async Task<HttpResponseMessage> Request(HttpRequestMessage request,
                                                        CancellationToken token)
        {
            CheckConnectionValid();

            using var _ = await streamLock.LockAsync(token).ConfigureAwait(false);
            Log.Debug("Making call {httpMethod} to {target}", request.Method, request.RequestUri);

            var response = await httpOperationOnStream.Request(request, token);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Log.Error("Call to {uri} failed with {code} and {error}",
                            request.RequestUri, response.StatusCode, error);
            }
            return response;
        }

        private async ValueTask TryParseHapStatus(string target, HttpResponseMessage response)
        {
            try
            {
                var jsonData = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var accessoryError = JsonConvert.DeserializeObject<AccessoryError>(jsonData);
                if (accessoryError != null)
                {
                    Log.Error("Unexpected {StatusCode} response with {HapError} for {target} for {EndPoint",
                                response.StatusCode, accessoryError.Status, target, Address);
                    throw new AccessoryException(accessoryError.Status);
                }
            }
            catch (AccessoryException)
            {
                throw;
            }
            catch (Exception)
            {
                //ignore any error here as we throw a gneric error later
            }
        }

        private const string JsonContentType = "application/hap+json";
        private const string TlvContentType = "application/pairing+tlv8";
        private readonly AsyncProducerConsumerQueue<HttpResponseMessage> eventQueue = new();
        private readonly Device homeKitDeviceInformation;
        private readonly AsyncLock streamLock = new();
        private TcpClient? client;
        private HttpOperationOnStream? httpOperationOnStream;
    }
}