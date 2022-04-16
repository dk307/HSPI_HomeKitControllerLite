using HomeKit.Model;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static HomeKit.SecureConnection;

#nullable enable

namespace HomeKit
{
    internal sealed record DeviceConnectionChangedArgs(bool Connected);

    internal sealed class SecureConnectionManager
    {
        public delegate void DeviceConnectionChangedHandler(object sender, DeviceConnectionChangedArgs e);

        public event AccessoryValueChangedHandler? AccessoryValueChangedEvent;
        public event DeviceConnectionChangedHandler? DeviceConnectionChangedEvent;
        public SecureConnection Connection
        {
            get
            {
                var connectionCopy = connection;
                if (connectionCopy is null)
                {
                    throw new InvalidOperationException($"Not connected to the Homekit Device");
                }
                return connectionCopy;
            }
        }

        public string DisplayNameForLog => lastDisplayName ?? "<Not connected>";

        public async Task ConnectionAndListen(PairingDeviceInfo info,
                                              IPEndPoint fallbackEndPoint,
                                              CancellationToken token)
        {
            try
            {
                EnqueueConnectionEvent(false);

                if (connection != null)
                {
                    connection.AccessoryValueChangedEvent -= AccessoryValueChangedEventForward;
                    connection.Dispose();
                }

                SecureConnection secureHomeKitConnection = new(info);
                var listenTask = await secureHomeKitConnection.ConnectAndListen(fallbackEndPoint, token).ConfigureAwait(false);

                lastDisplayName = secureHomeKitConnection.DisplayName;

                Interlocked.Exchange(ref connection, secureHomeKitConnection);
                secureHomeKitConnection.AccessoryValueChangedEvent += AccessoryValueChangedEventForward;

                var eventProcessTask = await secureHomeKitConnection.TrySubscribeAll(token);

                EnqueueConnectionEvent(true);

                //listen and process events
                while (!token.IsCancellationRequested)
                {
                    var delay = info.PollingTimeSpan?.TotalMilliseconds ?? -1;
                    var waitTask = Task.Delay((int)delay, token);
                    var finishedTask = await Task.WhenAny(listenTask, eventProcessTask, waitTask).ConfigureAwait(false);

                    if (waitTask == finishedTask)
                    {
                        await secureHomeKitConnection.RefreshValues(pollingIids, token).ConfigureAwait(false);
                    }
                    else
                    {
                        await finishedTask.ConfigureAwait(false);
                    }
                }

                await listenTask.ConfigureAwait(false);
                await eventProcessTask.ConfigureAwait(false);
            }
            finally
            {
                EnqueueConnectionEvent(false);
            }
        }

        public void SetPolling(IEnumerable<AidIidPair>? iids)
        {
            Interlocked.Exchange(ref pollingIids, iids);
        }

        private void AccessoryValueChangedEventForward(object sender, AccessoryValueChangedArgs e)
        {
            this.AccessoryValueChangedEvent?.Invoke(this, e);
        }

        private void EnqueueConnectionEvent(bool connection)
        {
            if (lastConnectionEvent != connection)
            {
                lastConnectionEvent = connection;
                DeviceConnectionChangedEvent?.Invoke(this, new DeviceConnectionChangedArgs(connection));
            }
        }

        private volatile SecureConnection? connection;
        private bool? lastConnectionEvent;
        private string? lastDisplayName;
        private IEnumerable<AidIidPair>? pollingIids;
    }
}