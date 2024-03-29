﻿using HomeKit;
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
            Utils.TaskHelper.StartAsyncWithErrorChecking(Invariant($"Device RefId(s) {name}"),
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
                var (canProcess, valueToSend) = pair.Value.GetValueToSend(colSend.TargetRef, colSend.ControlValue);

                if (canProcess)
                {
                    if (valueToSend != null)
                    {
                        Log.Information("Updated {name} with {value}", manager.DisplayNameForLog, valueToSend);
                        await manager.Connection.PutCharacteristic(valueToSend, cancellationToken).ConfigureAwait(false);
                        Log.Information("Updated {name} with {value}", manager.DisplayNameForLog, valueToSend);

                        await manager.Connection.RefreshValues(pollingIids.Add(valueToSend), token).ConfigureAwait(false);
                    }
                    return true;
                }
            }

            return false;
        }

        public async Task Unpair(CancellationToken token)
        {
            Log.Debug("Unpairing {name}", manager.DisplayNameForLog);
            await manager.Connection.RemovePairing(token).ConfigureAwait(false);
            Log.Information("Unpaired {name}", manager.DisplayNameForLog);
        }

        public async Task<bool> CanRefresh(int devOrFeatRef, CancellationToken token)
        {
            var devices = hsDevices;
            foreach (var pair in devices)
            {
                var aidIidPair = pair.Value.GetCharacteristicAidIidValue(devOrFeatRef);

                if (aidIidPair is not null)
                {
                    await manager.Connection.RefreshValues(new List<AidIidPair> { aidIidPair }, token).ConfigureAwait(false);
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
            List<ulong> invalidEnabledCharacteristics = new();
            foreach (var enabledCharacteristic in enabledCharacteristics)
            {
                var (service, characteristic) = accessory.FindCharacteristic(enabledCharacteristic);

                if ((service == null) || (characteristic == null))
                {
                    Log.Warning("Enabled Characteristic {id} not found on {name}", enabledCharacteristic, manager.DisplayNameForLog);
                    invalidEnabledCharacteristics.Add(enabledCharacteristic);
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
            RemoveDeletedCharacteristics(accessory, device, enabledCharacteristics);

            var hsHomeKirRootDevice = new HsHomeKitRootDevice(HS,
                                                              refId,
                                                              new HsHomeKitConnectedFeatureDevice(HS, connectedRefId),
                                                              featureRefIds);

            if (invalidEnabledCharacteristics.Count > 0)
            {
                hsHomeKirRootDevice.SetEnabledCharacteristics(enabledCharacteristics.Except(invalidEnabledCharacteristics));
            }

            return hsHomeKirRootDevice;
        }

        private void RemoveDeletedCharacteristics(Accessory accessory,
                                                  HomeSeer.PluginSdk.Devices.HsDevice device,
                                                  ImmutableSortedSet<ulong> enabledCharacteristics)
        {
            foreach (var feature in device.Features)
            {
                var typeData = HsHomeKitFeatureDevice.GetTypeData(feature.PlugExtraData);

                if (typeData.Type == HsHomeKitFeatureDevice.FeatureType.Characteristics &&
                    typeData.Iid != null)
                {
                    bool delete = false;
                    if (!enabledCharacteristics.Contains(typeData.Iid.Value))
                    {
                        Log.Information("Deleting {featureName} for {deviceName} because it is not enabled", feature.Name, device.Name);
                        delete = true;
                    }
                    if (!delete && accessory.FindCharacteristic(typeData.Iid.Value).Item2 == null)
                    {
                        Log.Information("Deleting {featureName} for {deviceName} because not found on homekit device", feature.Name, device.Name);
                        delete = true;
                    }
                    if (delete)
                    {
                        HS.DeleteFeature(feature.Ref);
                    }
                }
            }
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

        private void DeviceConnectionChangedEvent(object sender,
                                                  DeviceConnectionChangedArgs e)
        {
            if (e.Connected)
            {
                Log.Information("Connected to {name}", manager.DisplayNameForLog);
                if (this.hsDevices.Count == 0)
                {
                    CreateFeaturesAndDevices();

                    // update the devices that need to polled
                    CalculatePollingAndSubscribeAidIid();
                }

                // update last connected address
                foreach (var hsDevice in this.hsDevices.Values)
                {
                    var connection = manager.Connection;
                    hsDevice.SetTransientAccesssoryValues(connection.Address,
                                                  connection.DeviceReportedInfo.Accessories.First(x => x.Aid == hsDevice.Aid));
                }
            }
            else
            {
                Log.Information("Disconnected from {name}", manager.DisplayNameForLog);
            }

            // update connected state after everything is done
            foreach (var rootDevice in this.hsDevices)
            {
                rootDevice.Value.SetConnectedState(e.Connected);
            }
        }

        private void CalculatePollingAndSubscribeAidIid()
        {
            var accessoryInfo = manager.Connection.DeviceReportedInfo;

            List<AidIidPair> subscribe = new();
            List<AidIidPair> polling = new();
            foreach (var accessory in accessoryInfo.Accessories)
            {
                //filter by what device features exist
                foreach (var iid in hsDevices[accessory.Aid].CharacteristicFeatures.Keys)
                {
                    var characteristic = accessory.FindCharacteristic(iid).Item2;

                    if (characteristic?.SupportsNotifications ?? false)
                    {
                        subscribe.Add(new AidIidPair(accessory.Aid, iid));
                    }
                    else if (characteristic?.Permissions.Contains(CharacteristicPermissions.PairedRead) ?? false)
                    {
                        polling.Add(new AidIidPair(accessory.Aid, iid));
                    }
                }
            }

            Interlocked.Exchange(ref this.pollingIids, polling.ToImmutableList());
            Interlocked.Exchange(ref this.subscribeIids, subscribe.ToImmutableList());

            manager.SetSubscribeAndPollingAidIids(new SubscribeAndPollingAidIids(this.subscribeIids, this.pollingIids));
        }

        private async Task UpdateDeviceProperties()
        {
            //open aid == Accessory.MainAid device or first
            int refId = originalRefIds.Select(x => (int?)x).First(refId => HsHomeKitRootDevice.GetAid(HS, refId!.Value) == Accessory.MainAid) ??
                        originalRefIds.First();

            var pairingInfo = HsHomeKitBaseRootDevice.GetPairingInfo(HS, refId);
            var fallbackAddress = HsHomeKitBaseRootDevice.GetFallBackAddress(HS, refId);

            await manager.ConnectionAndListen(pairingInfo,
                                              fallbackAddress,
                                              cancellationToken).ConfigureAwait(false);
        }

        private readonly CancellationToken cancellationToken;
        private readonly IHsController HS;
        private readonly SecureConnectionManager manager = new();
        private readonly IEnumerable<int> originalRefIds;

        // aid to device dict
        private ImmutableDictionary<ulong, HsHomeKitRootDevice> hsDevices =
                             ImmutableDictionary<ulong, HsHomeKitRootDevice>.Empty;

        private ImmutableList<AidIidPair> subscribeIids = ImmutableList<AidIidPair>.Empty;
        private ImmutableList<AidIidPair> pollingIids = ImmutableList<AidIidPair>.Empty;
    }
}