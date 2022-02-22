using HomeKit;
using HomeKit.Model;
using HomeSeer.PluginSdk;
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
    internal sealed class HomeKitDevice
    {
        public HomeKitDevice(IHsController hsController,
                             IEnumerable<int> refIds,
                             CancellationToken cancellationToken)
        {
            if (!refIds.Any())
            {
                throw new ArgumentException(nameof(refIds));
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

        private void AccessoryValueChangedEvent(object sender, AccessoryValueChangedArgs e)
        {
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
                int index = device.Features.FindIndex(
                    x =>
                    {
                        var typeData = HsHomeKitFeatureDevice.GetTypeData(x.PlugExtraData);
                        return typeData.Iid == enabledCharacteristic &&
                               typeData.Type == HsHomeKitFeatureDevice.FeatureType.Characteristics;
                    });

                if (index == -1)
                {
                    var (service, characteristic) = accessory.FindCharacteristic(enabledCharacteristic);

                    if ((service == null) || (characteristic == null))
                    {
                        Log.Warning("Enabled Characteristic not found on {name}", manager.DisplayNameForLog);
                        continue;
                    }

                    int featureRefId = HsHomeKitDeviceFactory.CreateFeature(HS,
                                                                          refId,
                                                                          service.Type,
                                                                          characteristic);
                    HsHomeKitCharacteristicFeatureDevice item = new(HS, featureRefId);
                    featureRefIds.Add(item);

                    Log.Information("Created {featureName} for {deviceName}", item.NameForLog, device.Name);
                }
                else
                {
                    var feature = device.Features[index];
                    Log.Debug("Found {featureName} for {deviceName}", feature.Name, device.Name);
                    featureRefIds.Add(new HsHomeKitCharacteristicFeatureDevice(HS, feature.Ref));
                }
            }

            // delete removed ones
            foreach (var feature in device.Features)
            {
                if (!enabledCharacteristics.Contains((ulong)feature.Ref))
                {
                    var typeData = HsHomeKitFeatureDevice.GetTypeData(feature.PlugExtraData);
                    if (typeData.Type == HsHomeKitFeatureDevice.FeatureType.Characteristics)
                    {
                        Log.Information("Deleting {featureName} for {deviceName}", feature.Name, device.Name);
                        HS.DeleteFeature(feature.Ref);
                    }
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

            Dictionary<int, HsHomeKitRootDevice> rootDevices = new();
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
                rootDevices[refId] = rootDevice;
            }

            // check for new accessories on device and create them
            foreach (var accessory in deviceReportedInfo.Accessories)
            {
                var found = rootDevices.Values.Any(x => x.GetAid() == accessory.Aid);
                if (!found)
                {
                    Log.Warning("Found a new accessory from the homekit device {name}. Creating new device in Homeseer.",
                                manager.DisplayNameForLog);

                    int refId = HsHomeKitDeviceFactory.CreateHsDevice(HS,
                                                manager.Connection.PairingInfo,
                                                manager.Connection.Address,
                                                accessory);

                    var rootDevice = CreateAndUpdateFeatures(accessory, refId);
                    rootDevices[refId] = rootDevice;
                }
            }

            Interlocked.Exchange(ref this.hsDevices, rootDevices.ToImmutableDictionary());
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
                }

                // update last connected address
                foreach (var rootDevice in this.hsDevices)
                {
                    rootDevice.Value.SetFallBackAddress(manager.Connection.Address);
                }
            }
            else
            {
                Log.Information("Disconnected from {name}", manager.DisplayNameForLog);
            }

            // update connected state
            foreach (var rootDevice in this.hsDevices)
            {
                rootDevice.Value.SetConnectedState(e.Connected);
            }
        }

        private async Task UpdateDeviceProperties()
        {
            //open first device
            int refId = originalRefIds.First();
            var pairingInfo = HsHomeKitRootDevice.GetPairingInfo(HS, refId);
            var fallbackAddress = HsHomeKitRootDevice.GetFallBackAddress(HS, refId);

            await manager.ConnectionAndListen(pairingInfo,
                                              fallbackAddress,
                                              cancellationToken).ConfigureAwait(false);
        }

        private readonly CancellationToken cancellationToken;
        private readonly IHsController HS;
        private readonly SecureConnectionManager manager = new();
        private readonly IEnumerable<int> originalRefIds;
        private ImmutableDictionary<int, HsHomeKitRootDevice> hsDevices = ImmutableDictionary<int, HsHomeKitRootDevice>.Empty;
    }
}