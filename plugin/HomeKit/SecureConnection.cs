using HomeKit.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace HomeKit
{
    internal abstract record ChangedEvent();

    internal sealed record DeviceConnectionChangedEvent(bool Connected) : ChangedEvent;
    internal sealed record AccessoryValueChangedEvent(ulong Aid, ulong Iid, object? Value) : ChangedEvent;

    internal sealed class SecureConnection : Connection
    {
        public SecureConnection(PairingDeviceInfo pairingInfo)
            : base(pairingInfo.DeviceInformation)
        {
            this.pairingInfo = pairingInfo;
        }

        public DeviceReportedInfo? DeviceReportedInfo { get; private set; }

        public override async Task<Task> ConnectAndListen(CancellationToken token)
        {
            using var _ = await connectionLock.LockAsync(token).ConfigureAwait(false);

            var listenTask = await base.ConnectAndListen(token).ConfigureAwait(false);
            try
            {
                var pairing = new Pairing(this);
                var sessionKeys = await pairing.GetSessionKeys(pairingInfo, token);

                Log.Debug("Exchanged Session Keys, switching to encrypted for {Name}", DisplayName);

                ChaChaReadTransform chaChaReadStream = new(sessionKeys.AccessoryToControllerKey.AsSpan());
                ChaChaWriteTransform chaChaWriteStream = new(sessionKeys.ControllerToAccessoryKey.AsSpan());

                UpdateTransforms(chaChaReadStream, chaChaWriteStream);

                DeviceReportedInfo = await GetAccessories(token).ConfigureAwait(false);
                Log.Debug("Encrypted connection complete for {Name}", DisplayName);
                return listenTask;
            }
            catch (Exception)
            {
                Disconnect();
                throw;
            }
        }

        public async Task RemovePairing(CancellationToken token)
        {
            CheckHasDeviceInfo();

            await TryUnsubscribeAll(token).ConfigureAwait(false);

            var pairing = new Pairing(this);
            await pairing.RemovePairing(pairingInfo, token).ConfigureAwait(false);
            Log.Information("Removed Pairing for {Name}", DisplayName);
        }

        public async Task TrySubscribeAll(AsyncProducerConsumerQueue<ChangedEvent> changedEventQueue,
                                          CancellationToken token)
        {
            using var _ = await connectionLock.LockAsync(token).ConfigureAwait(false);
            CheckHasDeviceInfo();
            Interlocked.Exchange(ref this.changedEventQueue, changedEventQueue);
            foreach (var accessory in this.DeviceReportedInfo.Accessories)
            {
                var neededSubscriptions = accessory.Services.Values.SelectMany(
                                        s => s.Characteristics.Values.Where(c => c.SupportsNotifications))
                                        .Select(x => new Subscription(accessory.Aid, x.Iid));

                await SendExistingValuesToQueue(changedEventQueue, neededSubscriptions).ConfigureAwait(false);

                var changedSubscriptions = await ChangeSubscription(neededSubscriptions, true, token).ConfigureAwait(false);

                foreach (var subscription in changedSubscriptions)
                {
                    subscriptionsToDevice.Add(subscription);
                }

                if (neededSubscriptions.Count() != changedSubscriptions.Count)
                {
                    Log.Warning("Some of the device subscriptions failed for {Name}:{accessory}", DisplayName, accessory.Name);
                }

                Log.Information("Subscribed for {count} events from {Name}:{accessory}",
                                    subscriptionsToDevice.Count, DisplayName, accessory.Name);
            }

            if (processEventTask?.IsCompleted ?? true)
            {
                processEventTask = Task.Run(() => ProcessEvents(token), token);
            }
        }

        private async Task SendExistingValuesToQueue(AsyncProducerConsumerQueue<ChangedEvent> changedEventQueue, IEnumerable<Subscription> neededSubscriptions)
        {
            CheckHasDeviceInfo();

            foreach (var subscription in neededSubscriptions)
            {
                var characteristic = DeviceReportedInfo.FindCharacteristic(subscription.Aid, subscription.Iid);

                if (characteristic is not null)
                {
                    await changedEventQueue.EnqueueAsync(new AccessoryValueChangedEvent(subscription.Aid,
                                                                                  characteristic.Iid,
                                                                                  characteristic.Value)).ConfigureAwait(false);
                }
            }
        }

        public async Task TryUnsubscribeAll(CancellationToken token)
        {
            using var _ = await connectionLock.LockAsync(token).ConfigureAwait(false);
            foreach (var accessories in this.subscriptionsToDevice.ToLookup(x => x.Aid))
            {
                var changedSubscriptions = await ChangeSubscription(accessories, false, token).ConfigureAwait(false);

                foreach (var subscription in changedSubscriptions)
                {
                    subscriptionsToDevice.Remove(subscription);
                }
            }

            Log.Information("Unsubscribed for events from {Name}", DisplayName);
        }

        private async ValueTask<HashSet<Subscription>> ChangeSubscription(IEnumerable<Subscription> subscriptions,
                                                                          bool subscribe,
                                                                          CancellationToken token)
        {
            var doneSubscriptions = new HashSet<Subscription>();
            JObject request = new();

            JArray characteristicsRequest = new();
            foreach (var subscription in subscriptions)
            {
                var requestData = new JObject
                {
                    { "aid", subscription.Aid  },
                    { "iid", subscription.Iid  },
                    { "ev", subscribe? 1: 0 }
                };
                doneSubscriptions.Add(subscription);
                characteristicsRequest.Add(requestData);
            }

            request["characteristics"] = characteristicsRequest;

            var result = await HandleJsonRequest<JObject, JObject>(HttpMethod.Put, request,
                                                     "/characteristics",
                                                     cancellationToken: token).ConfigureAwait(false);

            if (result != null)
            {
                // some failed
                var characteristics = result["characteristics"] as JArray;

                if (characteristics != null)
                {
                    foreach (JToken row in characteristics)
                    {
                        var aid = (ulong?)row["aid"];
                        var iid = (ulong?)row["iid"];
                        var status = (string?)row["status"];

                        Log.Warning("Subscription to aid:{aid} iid:{iid} failed with {status} for {Name}",
                                        aid, iid, status, DisplayName);
                        if (aid != null && iid != null)
                        {
                            doneSubscriptions.Remove(new Subscription(aid.Value, iid.Value));
                        }
                    }
                }
            }

            return doneSubscriptions;
        }

        [MemberNotNull(nameof(DeviceReportedInfo))]
        private void CheckHasDeviceInfo()
        {
            if (DeviceReportedInfo == null)
            {
                throw new InvalidOperationException("Connection Never Made");
            }
        }

        private sealed record Subscription(ulong Aid, ulong Iid);

        private async ValueTask<DeviceReportedInfo> GetAccessories(CancellationToken token)
        {
            var info = await HandleJsonRequest<JObject, DeviceReportedInfo>(HttpMethod.Get, null,
                                                             "/accessories",
                                                             cancellationToken: token).ConfigureAwait(false);
            if (info == null)
            {
                throw new InvalidOperationException("Device Send empty device info");
            }
            return info;
        }

        private async Task ProcessEvents(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var eventMessage = await EventQueue.DequeueAsync(cancellationToken).ConfigureAwait(false);
                var jsonEventMessage = await eventMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

                var eventJsonObject = JsonConvert.DeserializeObject<JObject>(jsonEventMessage);

                var characteristics = eventJsonObject?["characteristics"] as JArray;

                if (characteristics != null)
                {
                    foreach (JToken row in characteristics)
                    {
                        var aid = (ulong?)row["aid"];
                        var iid = (ulong?)row["iid"];
                        var value = (string?)row["value"];

                        if (aid != null && iid != null && changedEventQueue != null)
                        {
                            var item = new AccessoryValueChangedEvent(aid.Value, iid.Value, value);
                            await changedEventQueue.EnqueueAsync(item, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            Log.Warning("Failed to Process event {event} for {Name}", jsonEventMessage, DisplayName);
                        }
                    }
                }
            }
        }

        private readonly PairingDeviceInfo pairingInfo;
        private readonly AsyncLock connectionLock = new();
        private readonly HashSet<Subscription> subscriptionsToDevice = new();
        private AsyncProducerConsumerQueue<ChangedEvent>? changedEventQueue;
        private Task? processEventTask;
    }
}