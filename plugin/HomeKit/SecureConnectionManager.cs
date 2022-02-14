﻿using HomeKit.Model;
using Nito.AsyncEx;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace HomeKit
{
    internal sealed class SecureConnectionManager
    {
        public void ConnectAndListenDevice(PairingDeviceInfo info,
                                           AsyncProducerConsumerQueue<ChangedEvent> changedEventQueue,
                                           CancellationToken token)
        {
            this.displayName = info.DeviceInformation.DisplayName;
            Hspi.Utils.TaskHelper.StartAsyncWithErrorChecking(
                $"{displayName} connection",
                () => ConnectionAndListenDeviceImpl(info, changedEventQueue, token),
                token,
                TimeSpan.FromSeconds(1));
        }

        public SecureConnection Connection
        {
            get
            {
                var connectionCopy = connection;
                if (connectionCopy is null)
                {
                    throw new InvalidOperationException($"Not connected to the Homekit Device {displayName}");
                }
                return connectionCopy;
            }
        }

        private static async ValueTask EnqueueConnectionEvent(AsyncProducerConsumerQueue<ChangedEvent> changedEventQueue,
                                                                      bool connection)
        {
            // dont use cancel event here
            await changedEventQueue.EnqueueAsync(new DeviceConnectionChangedEvent(connection)).ConfigureAwait(false);
        }

        private async Task ConnectionAndListenDeviceImpl(PairingDeviceInfo info,
                                                         AsyncProducerConsumerQueue<ChangedEvent> changedEventQueue,
                                                         CancellationToken token)
        {
            try
            {
                await EnqueueConnectionEvent(changedEventQueue, false).ConfigureAwait(false);
                SecureConnection secureHomeKitConnection = new(info);
                var listenTask = await secureHomeKitConnection.ConnectAndListen(token).ConfigureAwait(false);
                Interlocked.Exchange(ref connection, secureHomeKitConnection);
                var eventProcessTask = await secureHomeKitConnection.TrySubscribeAll(changedEventQueue, token);

                await EnqueueConnectionEvent(changedEventQueue, true).ConfigureAwait(false);

                //listen and process events
                var finishedTask = await Task.WhenAny(listenTask, eventProcessTask).ConfigureAwait(false);
                await finishedTask.ConfigureAwait(false);
                await listenTask.ConfigureAwait(false);
                await eventProcessTask.ConfigureAwait(false);
            }
            finally
            {
                await EnqueueConnectionEvent(changedEventQueue, false).ConfigureAwait(false);
            }
        }

        private volatile SecureConnection? connection;
        private string? displayName;
    }
}