using HomeKit.Model;
using Hspi.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using Serilog;
using System;
using System.Collections.Generic;
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
                    catch (Exception)
                    {
                        Log.Warning("Ping to {Name} failed", DisplayName);
                        return false;
                    }
                }
            }
            throw new InvalidOperationException("No Readable Value found");
        }

        public async Task RemovePairing(CancellationToken token)
        {
            CheckHasDeviceInfo();

            await TryUnsubscribeAll(token).ConfigureAwait(false);

            var pairing = new Pairing(this);
            await pairing.RemovePairing(pairingInfo, token).ConfigureAwait(false);
            Log.Information("Removed Pairing for {Name}", DisplayName);
        }

        internal record AidIidPair(ulong Aid, ulong Iid);

        public async Task<Task> TrySubscribeAll(CancellationToken token)
        {
            using var _ = await connectionLock.LockAsync(token).ConfigureAwait(false);
            CheckHasDeviceInfo();
            foreach (var accessory in this.DeviceReportedInfo.Accessories)
            {
                var neededSubscriptions = accessory.Services.Values.SelectMany(
                                        s => s.Characteristics.Values.Where(c => c.SupportsNotifications))
                                        .Select(x => new AidIidPair(accessory.Aid, x.Iid));

                var changedSubscriptions = await ChangeSubscription(neededSubscriptions, true, token).ConfigureAwait(false);

                foreach (var AidIidPair in changedSubscriptions)
                {
                    subscriptionsToDevice.Add(AidIidPair);
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

            return processEventTask;
        }

        public async Task TryUnsubscribeAll(CancellationToken token)
        {
            using var _ = await connectionLock.LockAsync(token).ConfigureAwait(false);
            foreach (var accessories in this.subscriptionsToDevice.ToLookup(x => x.Aid))
            {
                var changedSubscriptions = await ChangeSubscription(accessories, false, token).ConfigureAwait(false);

                foreach (var AidIidPair in changedSubscriptions)
                {
                    subscriptionsToDevice.Remove(AidIidPair);
                }
            }

            Log.Information("Unsubscribed for events from {Name}", DisplayName);
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
                    { "ev", subscribe? 1: 0 }
                };
                doneSubscriptions.Add(AidIidPair);
                characteristicsRequest.Add(requestData);
            }

            request["characteristics"] = characteristicsRequest;

            var result = await HandleJsonRequest<JObject, JObject>(HttpMethod.Put, request,
                                                     "/characteristics", string.Empty,
                                                     cancellationToken: token).ConfigureAwait(false);

            if (result != null && result["characteristics"] is JArray characteristics)
            {
                foreach (JToken row in characteristics)
                {
                    var aid = (ulong?)row["aid"];
                    var iid = (ulong?)row["iid"];
                    var status = (string?)row["status"];

                    Log.Warning("AidIidPair to aid:{aid} iid:{iid} failed with {status} for {Name}",
                                    aid, iid, status, DisplayName);
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
            var info = await HandleJsonRequest<JObject, DeviceReportedInfo>(HttpMethod.Get, null,
                                                             "/accessories", string.Empty,
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
                    EnqueueResults(result);
                }
                catch (Exception ex)
                {
                    Log.Warning("Failed to Process event with {message} for {Name} with {error}",
                                jsonEventMessage, DisplayName, ex.GetFullMessage());
                }
            }
        }

        public async Task RefreshValues(CancellationToken token)
        {
            foreach (var accessory in DeviceReportedInfo.Accessories)
            {
                var readableCharacterestics = accessory.Services.Values.SelectMany(
                    x => x.Characteristics.Values.Where(c => c.Permissions.Contains(CharacteristicPermissions.PairedRead)));

                var data = string.Join(",", readableCharacterestics.Select(x => Invariant($"{accessory.Aid}.{x.Iid}")));

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

        private const string CharacteristicsTarget = "/characteristics";
        private readonly AsyncLock connectionLock = new();
        private readonly PairingDeviceInfo pairingInfo;
        private readonly HashSet<AidIidPair> subscriptionsToDevice = new();
        private DeviceReportedInfo? deviceReportedInfo;
        private Task? processEventTask;
    }
}