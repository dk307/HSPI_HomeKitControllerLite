using HomeKit.Model;
using Serilog;
using System;
using System.Collections.Immutable;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static HomeKit.SecureConnection;

#nullable enable

namespace HomeKit
{
    internal sealed record DeviceConnectionChangedArgs(bool Connected);

    internal sealed record SubscribeAndPollingAidIids(ImmutableList<AidIidPair> Subscribe,
                                                      ImmutableList<AidIidPair> Polling);

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
                FireConnectionEvent(false);

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

                // create devices & features & setup polling & subscribe iids
                FireConnectionEvent(true);

                // subscribe before refresh
                Log.Debug("Subscribing to characteristics for {name}", lastDisplayName);
                var eventProcessTask = await secureHomeKitConnection.TrySubscribe(subscribeAndPollingAidIids.Subscribe, token);

                // get all values initially to refresh even the event ones.
                Log.Debug("Refreshing all values for {name}", lastDisplayName);
                await connection!.RefreshValues(null, token).ConfigureAwait(false);

                //listen and process events
                while (!token.IsCancellationRequested)
                {
                    var delay = info.PollingTimeSpan?.TotalMilliseconds ?? -1;
                    var waitTask = Task.Delay((int)delay, token);
                    var finishedTask = await Task.WhenAny(listenTask, eventProcessTask, waitTask).ConfigureAwait(false);

                    if (waitTask == finishedTask)
                    {
                        await secureHomeKitConnection.RefreshValues(subscribeAndPollingAidIids.Polling, token).ConfigureAwait(false);
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
                FireConnectionEvent(false);
            }
        }

        public void SetSubscribeAndPollingAidIids(SubscribeAndPollingAidIids value)
        {
            Interlocked.Exchange(ref this.subscribeAndPollingAidIids, value);
        }

        private void AccessoryValueChangedEventForward(object sender, AccessoryValueChangedArgs e)
        {
            this.AccessoryValueChangedEvent?.Invoke(this, e);
        }

        private void FireConnectionEvent(bool connection)
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
        private SubscribeAndPollingAidIids subscribeAndPollingAidIids = new(ImmutableList<AidIidPair>.Empty, ImmutableList<AidIidPair>.Empty);
    }
}