using HomeKit;
using HomeKit.Model;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices.Controls;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

#nullable enable

namespace Hspi.DeviceData
{
    // Class to link between HS & network interface
    internal sealed class HomeKitDevice
    {
        public HomeKitDevice(IHsController hsController,
                             IEnumerable<int> refIds,
                             CancellationToken cancellationToken)
        {
            if (!refIds.Any())
            {
                throw new ArgumentException("Is Empty", nameof(refIds));
            }

            this.HS = hsController;
            this.originalRefIds = refIds;
            this.cancellationToken = cancellationToken;

            manager.DeviceConnectionChangedEvent += DeviceConnectionChangedEvent;
            manager.AccessoryValueChangedEvent += AccessoryValueChangedEvent;

            string name = String.Join(",", refIds.Select(x => x.ToString(CultureInfo.InvariantCulture)));
            Utils.TaskHelper.StartAsyncWithErrorChecking(Invariant($"Device Start {name}"),
                                                         UpdateDeviceProperties,
                                                         cancellationToken,
                                                         TimeSpan.FromSeconds(15));
        }

        public async Task<bool> CanProcessCommand(ControlEvent colSend,
                                                  CancellationToken token)
        {
            var devices = hsDevices;

            foreach (var pair in devices)
            {
                var (canProcess, valueToSend) = pair.Value.GetValueToSend(colSend);

                if (canProcess)
                {
                    if (valueToSend != null)
                    {
                        await manager.Connection.PutCharacteristic(valueToSend, cancellationToken).ConfigureAwait(false);
                        Log.Information("Updated {value} for {name}", valueToSend, manager.DisplayNameForLog);

                        await manager.Connection.RefreshValues(pollingIids.Add(valueToSend), token).ConfigureAwait(false);
                    }
                    return true;
                }
            }

            return false;
        }

        private void AccessoryValueChangedEvent(object sender, AccessoryValueChangedArgs e)
        {
            Log.Debug("Update received from {name} with {value}", manager.DisplayNameForLog, e);
            if (hsDevices.TryGetValue(e.Aid, out var rootDevice))
            {
                rootDevice.SetValue(e.Iid, e.Value);
            }
            else
            {
                Log.Warning("Unknown update received for {value} for {name}", e, manager.DisplayNameForLog);
            }
        }

        private HsHomeKitRootDevice CreateAndUpdateFeatures(Accessory accessory,
                                                            int refId)
        {
            var device = HS.GetDeviceWithFeaturesByRef(refId);
            var enabledCharacteristics =
                HsHomeKitRootDevice.GetEnabledCharacteristic(device.PlugExtraData);

            int connectedRefId = HsHomeKitDeviceFactory.CreateAndUpdateConnectedFeature(HS, device);

            List<HsHomeKitCharacteristicFeatureDevice> featureRefIds = new();
            foreach (var enabledCharacteristic in enabledCharacteristics)
            {
                var (service, characteristic) = accessory.FindCharacteristic(enabledCharacteristic);

                if ((service == null) || (characteristic == null))
                {
                    Log.Warning("Enabled Characteristic {id} not found on {name}", enabledCharacteristic, manager.DisplayNameForLog);
                    continue;
                }

                int index = device.Features.FindIndex(
                    x =>
                    {
                        var typeData = HsHomeKitFeatureDevice.GetTypeData(x.PlugExtraData);
                        return typeData.Iid == enabledCharacteristic &&
                               typeData.Type == HsHomeKitFeatureDevice.FeatureType.Characteristics;
                    });

                if (index == -1)
                {
                    int featureRefId = HsHomeKitDeviceFactory.CreateFeature(HS,
                                                                            refId,
                                                                            service.Type,
                                                                            characteristic);
                    HsHomeKitCharacteristicFeatureDevice item = new(HS, featureRefId, characteristic.Format, characteristic.DecimalPlaces);
                    featureRefIds.Add(item);

                    Log.Information("Created {featureName} for {deviceName}", item.NameForLog, device.Name);
                }
                else
                {
                    var feature = device.Features[index];
                    Log.Debug("Found {featureName} for {deviceName}", feature.Name, device.Name);
                    featureRefIds.Add(new HsHomeKitCharacteristicFeatureDevice(HS, feature.Ref, characteristic.Format, characteristic.DecimalPlaces));
                }
            }

            // delete removed ones
            foreach (var feature in device.Features)
            {
                var typeData = HsHomeKitFeatureDevice.GetTypeData(feature.PlugExtraData);
                if (typeData.Type == HsHomeKitFeatureDevice.FeatureType.Characteristics &&
                    (typeData.Iid == null || !enabledCharacteristics.Contains(typeData.Iid.Value)))
                {
                    Log.Information("Deleting {featureName} for {deviceName}", feature.Name, device.Name);
                    HS.DeleteFeature(feature.Ref);
                }
            }

            return new HsHomeKitRootDevice(HS,
                                           refId,
                                           new HsHomeKitConnectedFeatureDevice(HS, connectedRefId),
                                           featureRefIds);
        }

        private void CreateFeaturesAndDevices()
        {
            var deviceReportedInfo = manager.Connection.DeviceReportedInfo;

            Dictionary<ulong, HsHomeKitRootDevice> rootDevices = new();
            foreach (var refId in originalRefIds)
            {
                var aid = HsHomeKitRootDevice.GetAid(HS, refId);
                var accessory = deviceReportedInfo.Accessories.FirstOrDefault(x => x.Aid == aid);

                if (accessory == null)
                {
                    Log.Warning("A device {name} found in Homeseer which is not found in Homekit Device",
                                HS.GetNameByRef(refId));
                    continue;
                }

                var rootDevice = CreateAndUpdateFeatures(accessory, refId);
                rootDevices[rootDevice.Aid] = rootDevice;
            }

            // check for new accessories on device and create them
            foreach (var accessory in deviceReportedInfo.Accessories)
            {
                var found = rootDevices.Values.Any(x => x.Aid == accessory.Aid);
                if (!found)
                {
                    Log.Information("Found a new accessory from the homekit device {name}. Creating new device in Homeseer.",
                                     manager.DisplayNameForLog);

                    int refId = HsHomeKitDeviceFactory.CreateDevice(HS,
                                                manager.Connection.PairingInfo,
                                                manager.Connection.Address,
                                                accessory);

                    var rootDevice = CreateAndUpdateFeatures(accessory, refId);
                    rootDevices[rootDevice.Aid] = rootDevice;
                }
            }

            Interlocked.Exchange(ref this.hsDevices, rootDevices.ToImmutableDictionary());
            Log.Information("Devices ready and listening for {name}", manager.DisplayNameForLog);
        }

        private async void DeviceConnectionChangedEvent(object sender,
                                                        DeviceConnectionChangedArgs e)
        {
            if (e.Connected)
            {
                Log.Information("Connected to {name}", manager.DisplayNameForLog);
                if (this.hsDevices.Count == 0)
                {
                    CreateFeaturesAndDevices();

                    // get all values initially to refresh even the vent ones.
                    await manager.Connection.RefreshValues(null, cancellationToken).ConfigureAwait(false);

                    // update the devices that need to polled
                    SetupPollingForNonEventCharacteristics();
                }

                // update last connected address
                foreach (var pair in this.hsDevices)
                {
                    var connection = manager.Connection;
                    pair.Value.SetTransientAccesssoryValues(connection.Address,
                                                  connection.DeviceReportedInfo.Accessories.First(x => x.Aid == pair.Value.Aid));
                }
            }
            else
            {
                Log.Debug("Disconnected from {name}", manager.DisplayNameForLog);
            }

            // update connected state after everything is done
            foreach (var rootDevice in this.hsDevices)
            {
                rootDevice.Value.SetConnectedState(e.Connected);
            }
        }

        private void SetupPollingForNonEventCharacteristics()
        {
            var accessoryInfo = manager.Connection.DeviceReportedInfo;
            var subscribedMap = manager.Connection.SubscriptionsToDevice.ToLookup(x => x.Aid);

            List<AidIidPair> polling = new();
            foreach (var aid in accessoryInfo.Accessories.Select(x => x.Aid))
            {
                var allFeatureDevices = hsDevices[aid].CharacteristicFeatures.Keys;
                var subscribedMapForAccessory = subscribedMap[aid];
                var nonSubscribedFeatures = allFeatureDevices.Where(iid => !subscribedMapForAccessory.Any(x => x.Iid == iid));

                polling.AddRange(nonSubscribedFeatures.Select(x => new AidIidPair(aid, x)));
            }

            var aidIidPairs = polling.ToImmutableList();
            Interlocked.Exchange(ref this.pollingIids, aidIidPairs);
            manager.SetPolling(aidIidPairs);
        }

        private async Task UpdateDeviceProperties()
        {
            //open aid == 1 device or first
            int refId = originalRefIds.Select(x => (int?)x).First(refId => HsHomeKitRootDevice.GetAid(HS, refId!.Value) == 1) ??
                        originalRefIds.First();
            var pairingInfo = HsHomeKitBaseRootDevice.GetPairingInfo(HS, refId);
            var fallbackAddress = HsHomeKitBaseRootDevice.GetFallBackAddress(HS, refId);

            await manager.ConnectionAndListen(pairingInfo,
                                              fallbackAddress,
                                              TimeSpan.FromSeconds(30),
                                              cancellationToken).ConfigureAwait(false);
        }

        private readonly CancellationToken cancellationToken;
        private readonly IHsController HS;
        private readonly SecureConnectionManager manager = new();
        private readonly IEnumerable<int> originalRefIds;

        // aid to device dict
        private ImmutableDictionary<ulong, HsHomeKitRootDevice> hsDevices =
                             ImmutableDictionary<ulong, HsHomeKitRootDevice>.Empty;

        private ImmutableList<AidIidPair> pollingIids = ImmutableList<AidIidPair>.Empty;
    }
}