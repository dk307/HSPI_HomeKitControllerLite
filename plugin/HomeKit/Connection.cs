﻿using HomeKit.Exceptions;
using HomeKit.Http;
using HomeKit.Model;
using Hspi.Utils;
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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace HomeKit
{
    internal abstract class Connection : IDisposable
    {
        protected Connection(DeviceId deviceInformation, bool enableDevicePolling)
        {
            this.homeKitDeviceInformation = deviceInformation;
            this.enableDevicePolling = enableDevicePolling;
        }

        public IPEndPoint Address
        {
            get => address ?? throw new InvalidOperationException("Connection never made");
            private set => address = value;
        }

        public virtual bool Connected
        {
            get
            {
                try
                {
                    var connected = client?.Connected ?? false;

                    // Detect if client disconnected
                    if (connected &&
                        !(client!.Client.Poll(1, SelectMode.SelectRead) && client.Client.Available == 0))
                    {
                        return true;
                    }
                    return connected;
                }
                catch (Exception)
                {
                    // Connected check throws
                    return false;
                }
            }
        }

        public DeviceFeature DeviceFeature => homeKitDeviceInformation.Feature;
        public DeviceId DeviceInformation => homeKitDeviceInformation;
        public string DisplayName => homeKitDeviceInformation.DisplayName;

        protected AsyncProducerConsumerQueue<HttpResponseMessage> EventQueue => eventQueue;
        protected NetworkStream UnderLyingStream => client?.GetStream() ?? throw new InvalidOperationException("Client not connected");

        public virtual async Task<Task> ConnectAndListen(IPEndPoint fallbackAddress,
                                                         CancellationToken token)
        {
            var discoveredInfo = await HomeKitDiscover.DiscoverDeviceById(
                                              homeKitDeviceInformation.Id,
                                              TimeSpan.FromSeconds(15),
                                              token);

            if (discoveredInfo == null)
            {
                Log.Warning("Did not find {name} on the network. Using default address:{address}.",
                             DisplayName, fallbackAddress);
            }

            Address = discoveredInfo?.Address ?? fallbackAddress;

            client = new TcpClient()
            {
                NoDelay = true,
                LingerState = new LingerOption(false, 0),
            };

            Log.Information("Connecting to {Name} at {EndPoint}", DisplayName, Address);
            await client.ConnectAsync(Address.Address, Address.Port).ConfigureAwait(false);

            if (this.enableDevicePolling)
            {
                var keepAliveTime = TimeSpan.FromSeconds(30);
                var keepAliveInterval = TimeSpan.FromSeconds(30);
                SetSocketKeepAlive(client.Client, keepAliveTime, keepAliveInterval);
            }

            Log.Information("Connected to {Name} at {EndPoint}", DisplayName, Address);

            HttpOperationOnStream value = new(UnderLyingStream, eventQueue);
            return StartListening(value, token);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal async Task<IEnumerable<TlvValue>> PostTlv(IEnumerable<TlvValue> tlvList,
                                  string target,
                                  string query,
                                  string contentType = TlvContentType,
                                  CancellationToken cancellationToken = default)
        {
            var content = Tlv8.Encode(tlvList);

            var response = await Request(HttpMethod.Post, target, query,
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

        protected void Disconnect()
        {
            this.client?.Close();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                client?.Dispose();
            }
        }

        protected async Task<R?> HandleJsonRequest<T, R>(HttpMethod httpMethod,
                                                         T? value,
                                                         string target,
                                                         string query,
                                                         string contentType = JsonContentType,
                                                         CancellationToken cancellationToken = default) where R : class
        {
            byte[]? bytesContent = null;
            if (value != null)
            {
                var content = JsonConvert.SerializeObject(value, typeof(T), CreateJsonSerializer());
                bytesContent = Encoding.UTF8.GetBytes(content);
            }

            var response = await Request(httpMethod, target, query,
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
            return JsonConvert.DeserializeObject<R>(responseData, CreateJsonSerializer());
        }

        protected async Task<HttpResponseMessage> Request(HttpMethod httpMethod,
                                                          string target,
                                                          string query,
                                                          byte[]? content = null,
                                                          string? contentType = null,
                                                          CancellationToken token = default)
        {

            var builder = new UriBuilder
            {
                Port = Address.Port,
                Path = target,
                Query = query,
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

        private static JsonSerializerSettings CreateJsonSerializer()
        {
            return new()
            {
                Formatting = Formatting.None,
            };
        }
        private static void SetSocketKeepAlive(Socket socket,
                                               TimeSpan keepAliveTime,
                                               TimeSpan keepAliveInterval)
        {
            int size = Marshal.SizeOf((uint)0);
            byte[] keepAlive = new byte[size * 3];

            Buffer.BlockCopy(BitConverter.GetBytes((uint)1), 0, keepAlive, 0, size);
            Buffer.BlockCopy(BitConverter.GetBytes((uint)keepAliveTime.TotalMilliseconds), 0, keepAlive, size, size);
            Buffer.BlockCopy(BitConverter.GetBytes((uint)keepAliveInterval.TotalMilliseconds), 0, keepAlive, size * 2, size);
            socket.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);
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

            Log.Debug("Making call {httpMethod} to {target} with Body :{body}", request.Method, 
                            request.RequestUri,
                            await GetRequestBody(request).ConfigureAwait(false));

            var response = await httpOperationOnStream.Request(request, token);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Log.Error("Call to {uri} failed with {code} and {error}",
                            request.RequestUri, response.StatusCode, error);
            }
            return response;

            static async Task<string?> GetRequestBody(HttpRequestMessage request)
            {
                if (request.Content is ByteArrayContent byteArrayContent)
                {
                    var data = await byteArrayContent.ReadAsByteArrayAsync().ConfigureAwait(false);
                    return Encoding.UTF8.GetString(data);

                }
                return null;
            }
        }
        private async ValueTask TryParseHapStatus(string target, HttpResponseMessage response)
        {
            try
            {
                var jsonData = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var accessoryError = JsonConvert.DeserializeObject<AccessoryError>(jsonData);
                if (accessoryError != null)
                {
                    Log.Error("Unexpected {StatusCode} response with {HapError} for {target} for {EndPoint}",
                                response.StatusCode, accessoryError.Status, target, Address);
                    throw new AccessoryException(accessoryError.Status);
                }
            }
            catch (AccessoryException)
            {
                throw;
            }
            catch (Exception ex) when (!ex.IsCancelException())
            {
                //ignore any error here as we throw a gneric error later
            }
        }

        private const string JsonContentType = "application/hap+json";
        private const string TlvContentType = "application/pairing+tlv8";
        private readonly bool enableDevicePolling;
        private readonly AsyncProducerConsumerQueue<HttpResponseMessage> eventQueue = new();
        private readonly DeviceId homeKitDeviceInformation;
        private readonly AsyncLock streamLock = new();
        private IPEndPoint? address;
        private TcpClient? client;
        private HttpOperationOnStream? httpOperationOnStream;
    }
}