using HomeKit.Exceptions;
using HomeKit.Model;
using Hspi.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

#nullable enable

namespace HomeKit
{
    internal sealed record AccessoryValueChangedArgs(ulong Aid, ulong Iid, object? Value);

    internal sealed class SecureConnection : Connection
    {
        public SecureConnection(PairingDeviceInfo pairingInfo)
            : base(pairingInfo.DeviceInformation, pairingInfo.EnableKeepAliveForConnection)
        {
            this.pairingInfo = pairingInfo;
        }

        public delegate void AccessoryValueChangedHandler(object sender, AccessoryValueChangedArgs e);

        public event AccessoryValueChangedHandler? AccessoryValueChangedEvent;

        public DeviceReportedInfo DeviceReportedInfo
        {
            get => deviceReportedInfo ?? throw new InvalidOperationException("Connection never made");
            private set => deviceReportedInfo = value;
        }

        public PairingDeviceInfo PairingInfo => pairingInfo;

        public override async Task<Task> ConnectAndListen(IPEndPoint fallbackAddress, CancellationToken token)
        {
            using var _ = await connectionLock.LockAsync(token).ConfigureAwait(false);

            var listenTask = await base.ConnectAndListen(fallbackAddress, token).ConfigureAwait(false);
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

        public async Task<bool> Ping(CancellationToken token)
        {
            Log.Debug("Pinging {Name}", DisplayName);
            CheckHasDeviceInfo();

            foreach (var accessory in this.DeviceReportedInfo.Accessories)
            {
                var id = accessory.FindCharacteristic(ServiceType.AccessoryInformation,
                                                      CharacteristicType.Name);
                if (id != null)
                {
                    try
                    {
                        await this.HandleJsonRequest<JObject, JObject>(HttpMethod.Get,
                                                                       null,
                                                                       CharacteristicsTarget,
                                                                       Invariant($"id={accessory.Aid}.{id.Iid}"),
                                                                       cancellationToken: token);
                        Log.Debug("Ping to {Name} succeeded", DisplayName);

                        return true;
                    }
                    catch (Exception ex) when (!ex.IsCancelException())
                    {
                        Log.Warning("Ping to {Name} failed", DisplayName);
                        return false;
                    }
                }
            }
            throw new InvalidOperationException("No Readable Value found");
        }

        public async Task PutCharacteristic(AidIidValue id, CancellationToken token)
        {
            Log.Debug("Making {name} update to {value}", id);
            JObject request = new();

            JArray characteristicsRequest = new();
            var requestData = new JObject
                {
                    { "aid", id.Aid  },
                    { "iid", id.Iid  },
                    { "value", id.Value!= null ?JToken.FromObject(id.Value) : null }
                };
            characteristicsRequest.Add(requestData);
            request["characteristics"] = characteristicsRequest;

            var result = await HandleJsonRequest<JObject, JObject>(HttpMethod.Put,
                                                                   request,
                                                                   "/characteristics",
                                                                   string.Empty,
                                                                   cancellationToken: token).ConfigureAwait(false);

            if (result != null && result["characteristics"] is JArray characteristics)
            {
                var row = characteristics.FirstOrDefault(); // expect only one
                if (row != null)
                {
                    ParseHapStatus(row, out var aid, out var iid, out var status);

                    Log.Warning("Failed to update {Name} aid:{aid} iid:{iid} failed with {status}",
                                    DisplayName, aid, iid, status);

                    throw new AccessoryException(aid, iid, status);
                }
            }
        }

        public async Task RefreshValues(IEnumerable<AidIidPair>? iids, CancellationToken token)
        {
            // this lock prevents case where RefreshValues overlap & processing of the events overlap
            using var _ = await enqueueLock.LockAsync(token).ConfigureAwait(false);
            foreach (var accessory in DeviceReportedInfo.Accessories)
            {
                var readableCharacterestics = iids?.Where(x => x.Aid == accessory.Aid).Select(x => x.Iid) ??
                                              accessory.GetAllReadableCharacteristics().Select(x => x.Iid);

                if (!readableCharacterestics.Any())
                {
                    continue;
                }

                var data = string.Join(",", readableCharacterestics.Select(x => Invariant($"{accessory.Aid}.{x}")));

                var result = await this.HandleJsonRequest<JObject, CharacteristicsValuesList>(HttpMethod.Get,
                                                                                              null,
                                                                                              CharacteristicsTarget,
                                                                                              "id=" + data,
                                                                                              cancellationToken: token);
                if (result != null)
                {
                    EnqueueResults(result);
                }
                else
                {
                    Log.Warning("Failed to refresh values {values} for {name}", data, DisplayName);
                }
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

        public async Task<Task> TrySubscribe(IEnumerable<AidIidPair> subscribe, CancellationToken token)
        {
            using var _ = await connectionLock.LockAsync(token).ConfigureAwait(false);

            var builder = subscriptionsToDevice.ToBuilder();
            foreach (var neededSubscriptions in subscribe.ToLookup(x => x.Aid))
            {
                var changedSubscriptions = await ChangeSubscription(neededSubscriptions, true, token)
                                                 .ConfigureAwait(false);

                foreach (var pair in changedSubscriptions)
                {
                    builder.Add(pair);
                }

                if (neededSubscriptions.Count() != changedSubscriptions.Count) 
                {
                    Log.Warning("Some of the device subscriptions failed for {Name} Aid:{aid}", DisplayName, neededSubscriptions.Key);
                }

                Log.Information("Subscribed for {count} events from {Name} Aid:{value}",
                                 changedSubscriptions.Count, DisplayName, neededSubscriptions.Key);
            }

            Interlocked.Exchange(ref subscriptionsToDevice, builder.ToImmutableHashSet());

            if (processEventTask?.IsCompleted ?? true)
            {
                processEventTask = Task.Run(() => ProcessEvents(token), token);
            }

            return processEventTask;
        }

        public async Task TryUnsubscribeAll(CancellationToken token)
        {
            using var _ = await connectionLock.LockAsync(token).ConfigureAwait(false);

            var builder = subscriptionsToDevice.ToBuilder();

            foreach (var accessories in this.subscriptionsToDevice.ToLookup(x => x.Aid))
            {
                var changedSubscriptions = await ChangeSubscription(accessories, false, token).ConfigureAwait(false);

                foreach (var AidIidPair in changedSubscriptions)
                {
                    builder.Remove(AidIidPair);
                }
            }

            Interlocked.Exchange(ref subscriptionsToDevice, builder.ToImmutableHashSet());

            Log.Information("Unsubscribed for events from {Name}", DisplayName);
        }

        private static void ParseHapStatus(JToken row, out ulong? aid, out ulong? iid, out HAPStatus? status)
        {
            aid = (ulong?)row["aid"];
            iid = (ulong?)row["iid"];
            status = (HAPStatus?)row["status"]?.Value<Int64?>();
        }

        private async ValueTask<HashSet<AidIidPair>> ChangeSubscription(IEnumerable<AidIidPair> subscriptions,
                                                                        bool subscribe,
                                                                        CancellationToken token)
        {
            var doneSubscriptions = new HashSet<AidIidPair>();
            JObject request = new();

            JArray characteristicsRequest = new();
            foreach (var AidIidPair in subscriptions)
            {
                var requestData = new JObject
                {
                    { "aid", AidIidPair.Aid  },
                    { "iid", AidIidPair.Iid  },
                    { "ev", subscribe }
                };
                doneSubscriptions.Add(AidIidPair);
                characteristicsRequest.Add(requestData);
            }

            request["characteristics"] = characteristicsRequest;

            var result = await HandleJsonRequest<JObject, JObject>(HttpMethod.Put,
                                                                   request,
                                                                   "/characteristics",
                                                                   string.Empty,
                                                                   cancellationToken: token).ConfigureAwait(false);

            if (result != null && result["characteristics"] is JArray characteristics)
            {
                foreach (JToken row in characteristics)
                {
                    ParseHapStatus(row, out var aid, out var iid, out var status);

                    Log.Warning("Failed to change subscription for {name} aid:{aid} iid:{iid} failed with {status} for {Name}",
                                    DisplayName, aid, iid, status);
                    if (aid != null && iid != null)
                    {
                        doneSubscriptions.Remove(new AidIidPair(aid.Value, iid.Value));
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

        private void EnqueueResults(CharacteristicsValuesList result)
        {
            foreach (var value in result.Values)
            {
                var item = new AccessoryValueChangedArgs(value.Aid, value.Iid, value.Value);
                AccessoryValueChangedEvent?.Invoke(this, item);
            }
        }

        private async ValueTask<DeviceReportedInfo> GetAccessories(CancellationToken token)
        {
            var info = await HandleJsonRequest<JObject, DeviceReportedInfo>(HttpMethod.Get,
                                                                            null,
                                                                            "/accessories",
                                                                            string.Empty,
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

                try
                {
                    var result = JsonConvert.DeserializeObject<CharacteristicsValuesList>(jsonEventMessage);

                    // this lock prevents case where RefreshValues overlap & processing of the events overlap
                    using var _ = await enqueueLock.LockAsync(cancellationToken).ConfigureAwait(false);
                    EnqueueResults(result);
                }
                catch (Exception ex) when (!ex.IsCancelException())
                {
                    Log.Warning("Failed to Process event with {message} for {Name} with {error}",
                                jsonEventMessage, DisplayName, ex.GetFullMessage());
                }
            }
        }

        private const string CharacteristicsTarget = "/characteristics";
        private readonly AsyncLock connectionLock = new();
        private readonly AsyncLock enqueueLock = new();
        private readonly PairingDeviceInfo pairingInfo;
        private DeviceReportedInfo? deviceReportedInfo;
        private Task? processEventTask;
        private ImmutableHashSet<AidIidPair> subscriptionsToDevice = ImmutableHashSet<AidIidPair>.Empty;
    }
}