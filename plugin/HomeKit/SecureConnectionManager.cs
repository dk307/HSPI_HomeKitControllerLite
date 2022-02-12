using HomeKit.Model;
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
            Hspi.Utils.TaskHelper.StartAsyncWithErrorChecking(
                $"{info.DeviceInformation.DisplayName} connection",
                () => ConnectionAndListenDeviceImpl(info, changedEventQueue, token), token);
        }

        public async Task UnPair(CancellationToken token)
        {
            var secureHomeKitConnection = GetConnection();
            await secureHomeKitConnection.RemovePairing(token);
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
                await secureHomeKitConnection.TrySubscribeAll(changedEventQueue, token);

                await EnqueueConnectionEvent(changedEventQueue, true).ConfigureAwait(false);

                //listen and process events
                await listenTask.ConfigureAwait(false);
            }
            finally
            {
                await EnqueueConnectionEvent(changedEventQueue, false).ConfigureAwait(false);
            }
        }

        private SecureConnection GetConnection()
        {
            if (connection is null)
            {
                throw new InvalidOperationException("Not connected to the Device");
            }
            return connection;
        }

        private SecureConnection? connection;
    }
}