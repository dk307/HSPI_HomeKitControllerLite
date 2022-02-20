using HomeKit.Model;
using HomeKit.Utils;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi.DeviceData.HSMapping;
using Hspi.HomeKit.Utils;
using Hspi.Utils;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

#nullable enable

namespace Hspi.DeviceData
{
    internal sealed class HomeKitDeviceFactory
    {
        public enum DeviceType
        {
            Root = 0,
            OnlineStatus = 1,
            Characteristics = 2
        }

        public static NewFeatureData CreateFeature(HsDevice hsDevice,
                                                   ServiceType serviceType,
                                                   Characteristic characteristic)
        {
            var featureFactory = FeatureFactory.CreateFeature(PlugInData.PlugInId)
                                               .WithLocation(PlugInData.PlugInName)
                                               .WithExtraData(CreatePlugInExtraforDeviceType(DeviceType.Characteristics, characteristic.Iid));

            bool writable = characteristic.Permissions.Contains(CharacteristicPermissions.PairedWrite);

            if (!writable)
            {
                featureFactory = featureFactory.WithMiscFlags(EMiscFlag.StatusOnly);
            }

            var mapping = HSMappings.Value.Mappings?.FirstOrDefault(x => x.Iid == characteristic.Type.Id);

            featureFactory = SetName(characteristic, featureFactory, mapping);
            featureFactory = SetFeatureType(serviceType, featureFactory, mapping);

            var newData = featureFactory.PrepareForHsDevice(hsDevice.Ref);

            if (characteristic.Format != CharacteristicFormat.String)
            {
                if ((characteristic.ValidValues != null) &&
                    (characteristic.ValidValues.Count > 0))
                {
                    AddValidValuesGraphicsAndStatus(newData, serviceType, characteristic, writable, mapping);
                }
                else
                {
                    AddRangedGraphicsAndStatus(newData, serviceType, characteristic, writable, mapping);
                }
            }

            AddUnitSuffix(newData, characteristic);
            return newData;
        }

        public static int CreateHsDevice(IHsController hsController,
                                 PairingDeviceInfo pairingDeviceInfo,
                                 IPEndPoint fallbackAddress,
                                 Accessory accessory)
        {
            var extraData = CreateRootPlugInExtraData(pairingDeviceInfo,
                                                      fallbackAddress,
                                                      accessory.Aid);

            string friendlyName = pairingDeviceInfo.DeviceInformation.DisplayName ??
                                  accessory.Name ??
                                  pairingDeviceInfo.DeviceInformation.Model ??
                                  "HomeKit Device";

            var (deviceType, subFeatureType) = DetermineRootDeviceType(pairingDeviceInfo.DeviceInformation);
            var newDeviceData = DeviceFactory.CreateDevice(PlugInData.PlugInId)
                         .WithName(friendlyName)
                         .AsType(deviceType, subFeatureType)
                         .WithLocation(PlugInData.PlugInName)
                         .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange)
                         .WithExtraData(extraData)
                         .PrepareForHs();

            int refId = hsController.CreateDevice(newDeviceData);
            Log.Information("Created Device {friendlyName}", friendlyName);
            return refId;
        }

        private static PlugExtraData CreatePlugInExtraforDeviceType(DeviceType deviceType, ulong? iid = null)
        {
            var plugExtra = new PlugExtraData();
            DeviceTypeData value = new(deviceType, iid);
            plugExtra.AddNamed(DeviceTypePlugExtraTag,
                               JsonConvert.SerializeObject(value));
            return plugExtra;
        }

        private static void AddRangedGraphicsAndStatus(NewFeatureData newData,
                                               ServiceType serviceType,
                                               Characteristic characteristic,
                                               bool writable,
                                               HSMapping.HSMapping? mapping)
        {
            double minValue = characteristic.MinimumValue ??
                              characteristic.ValidValuesRange?[0] ??
                              double.MinValue;
            double maxValue = characteristic.MaximumValue ??
                              characteristic.ValidValuesRange?[1] ??
                              double.MaxValue;

            var rangeOptions = mapping?.RangeOptions;
            var rangeIcon = rangeOptions?.Icon;

            if (rangeIcon != null)
            {
                var statusGraphics = (StatusGraphicCollection)newData.Feature[EProperty.StatusGraphics];
                statusGraphics.Add(new StatusGraphic(CreateImagePath(rangeIcon),
                                                     minValue, maxValue));
            }

            if (writable)
            {
                var statusControls = (StatusControlCollection)newData.Feature[EProperty.StatusControls];

                int controlUse = rangeOptions?.EControlUses?.FirstOrDefault(x => x.ServiceIId == serviceType.Id)?.Value ?? (int)EControlUse.NotSpecified;
                statusControls.Add(new StatusControl(EControlType.TextBoxNumber)
                {
                    ControlUse = (EControlUse)controlUse,
                    TargetRange = new ValueRange(minValue, maxValue),
                });
            }
        }

        private static void AddUnitSuffix(NewFeatureData data,
                                         Characteristic characteristic)
        {
            if (characteristic.Unit != null)
            {
                var unitAttribute = EnumHelper.GetAttribute<UnitAttribute>(characteristic.Unit);
                var suffix = unitAttribute?.Unit;

                if (!string.IsNullOrWhiteSpace(suffix))
                {
                    data.Feature.Add(EProperty.AdditionalStatusData, new List<string>() { suffix! });

                    if (data.Feature[EProperty.StatusGraphics] is StatusGraphicCollection graphics)
                    {
                        foreach (var statusGraphic in graphics.Values)
                        {
                            statusGraphic.HasAdditionalData = true;

                            if (statusGraphic.IsRange)
                            {
                                statusGraphic.TargetRange.Suffix = " " + HsFeature.GetAdditionalDataToken(0);
                                statusGraphic.TargetRange.DecimalPlaces = 3;
                            }
                        }
                    }
                }
            }
        }

        private static void AddValidValuesGraphicsAndStatus(NewFeatureData newData,
                                                    ServiceType serviceType,
                                                    Characteristic characteristic,
                                                    bool writable,
                                                    HSMapping.HSMapping? mapping)
        {
            foreach (var value in characteristic.ValidValues)
            {
                var statusGraphics = (StatusGraphicCollection)newData.Feature[EProperty.StatusGraphics];
                var buttonMapping = mapping?.ButtonOptions?.FirstOrDefault(x => x.Value == value);
                statusGraphics.Add(new StatusGraphic(CreateImagePath(buttonMapping?.Icon ?? "default.png"),
                                                     value,
                                                     buttonMapping?.Name ?? value.ToString(CultureInfo.InvariantCulture)));

                if (writable)
                {
                    var statusControls = (StatusControlCollection)newData.Feature[EProperty.StatusControls];
                    int controlUse = buttonMapping?.EControlUses?.FirstOrDefault(x => x.ServiceIId == serviceType.Id)?.Value ?? (int)EControlUse.NotSpecified;
                    statusControls.Add(new StatusControl(EControlType.Button)
                    {
                        ControlUse = (EControlUse)controlUse,
                        Label = buttonMapping?.Name ?? value.ToString(CultureInfo.InvariantCulture),
                        TargetValue = value,
                    });
                }
            }
        }

        private static string CreateImagePath(string featureName)
        {
            return Path.ChangeExtension(Path.Combine(PlugInData.PlugInId, "images", featureName), "png");
        }

        private static PlugExtraData CreateRootPlugInExtraData(PairingDeviceInfo pairingDeviceInfo,
                                                               IPEndPoint fallbackAddress,
                                                               ulong aid)
        {
            var plugExtra = new PlugExtraData();
            plugExtra.AddNamed(PairInfoPlugExtraTag, JsonConvert.SerializeObject(pairingDeviceInfo, Formatting.Indented));
            plugExtra.AddNamed(FallbackAddressPlugExtraTag, JsonConvert.SerializeObject(fallbackAddress, Formatting.Indented, new IPEndPointJsonConverter()));
            plugExtra.AddNamed(AidPlugExtraTag, JsonConvert.SerializeObject(aid));
            DeviceTypeData value = new(DeviceType.Root);
            plugExtra.AddNamed(DeviceTypePlugExtraTag, JsonConvert.SerializeObject(value));
            return plugExtra;
        }

        private static (EDeviceType, int) DetermineRootDeviceType(Device device)
        {
            return device.CategoryIdentifier switch
            {
                DeviceCategory.Fans => (EDeviceType.Fan, 0),
                DeviceCategory.GarageDoorOpeners => (EDeviceType.Door, 0),
                DeviceCategory.Lighting => (EDeviceType.Light, 0),
                DeviceCategory.Locks => (EDeviceType.Lock, 0),
                DeviceCategory.Outlets => (EDeviceType.Outlet, 0),
                DeviceCategory.Switches => (EDeviceType.Switch, 0),
                DeviceCategory.Thermostats => (EDeviceType.Thermostat, 0),
                DeviceCategory.Doors => (EDeviceType.Door, 0),
                DeviceCategory.Windows => (EDeviceType.Window, 0),
                DeviceCategory.WindowCoverings => (EDeviceType.Window, 0),
                DeviceCategory.ProgrammableSwitches => (EDeviceType.Switch, 0),
                DeviceCategory.Sensors => (EDeviceType.Generic, (int)EGenericDeviceSubType.Sensor),
                _ => (EDeviceType.Generic, 0)
            };
        }

        private static DeviceTypeData? GetDeviceTypeFromPlugInData(PlugExtraData? plugInExtra)
        {
            if (plugInExtra != null && plugInExtra.NamedKeys.Contains(DeviceTypePlugExtraTag))
            {
                var data = plugInExtra[DeviceTypePlugExtraTag];
                return JsonConvert.DeserializeObject<DeviceTypeData>(data);
            }

            return null;
        }

        private static FeatureFactory SetFeatureType(ServiceType serviceType,
                                                                                                             FeatureFactory featureFactory,
                                     HSMapping.HSMapping? mapping)
        {
            var deviceTypeFromMapping = mapping?.DeviceTypes?.FirstOrDefault(x => x.ServiceIId == serviceType.Id);

            featureFactory = featureFactory.AsType(deviceTypeFromMapping?.FeatureType ?? EFeatureType.Generic,
                                                   deviceTypeFromMapping?.FeatureSubType ?? 0);
            return featureFactory;
        }

        private static FeatureFactory SetName(Characteristic characteristic,
                                      FeatureFactory featureFactory,
                                      HSMapping.HSMapping? mapping)
        {
            featureFactory = featureFactory.WithName(mapping?.Name ??
                                                     characteristic.Description ??
                                                     characteristic.Type.Id.ToString("D"));
            return featureFactory;
        }

        private int CreateAndUpdateConnectedFeature(IHsController hsController,
                                                    HsDevice device)
        {
            foreach (var feature in device.Features)
            {
                if (GetDeviceTypeFromPlugInData(feature.PlugExtraData)?.DeviceType == DeviceType.OnlineStatus)
                {
                    return feature.Ref;
                }
            }

            var newFeatureData = FeatureFactory.CreateFeature(PlugInData.PlugInId)
               .WithName("Connected")
               .WithMiscFlags(EMiscFlag.StatusOnly)
               .AsType(EFeatureType.Generic, 0)
               .WithExtraData(CreatePlugInExtraforDeviceType(DeviceType.OnlineStatus))
               .AddGraphicForValue(CreateImagePath("online"), OnValue, StatusOnline)
               .AddGraphicForValue(CreateImagePath("offline"), OffValue, StatusOffline)
               .PrepareForHsDevice(device.Ref);

            return hsController.CreateFeatureForDevice(newFeatureData);
        }

        public const string AidPlugExtraTag = "aid";

        public const string DeviceTypePlugExtraTag = "device.type";

        public const string FallbackAddressPlugExtraTag = "fallback.address";

        public const string PairInfoPlugExtraTag = "pairing.info";

        private const double OffValue = 0;

        private const double OnValue = 1;

        private const string StatusOffline = "Offline";

        private const string StatusOnline = "Online";

        private static readonly Lazy<HSMappings> HSMappings = new(() =>
                                                                                                                                                                                                   {
                                                                                                                                                                                                       string json = Encoding.UTF8.GetString(Resource.HSMappings);
                                                                                                                                                                                                       return JsonHelper.DeserializeObject<HSMappings>(json);
                                                                                                                                                                                                   }, true);

        private sealed record DeviceTypeData
        {
            public DeviceType DeviceType { get; init; }
            public ulong? Iid { get; init; }

            public DeviceTypeData(DeviceType deviceType, ulong? iid = null)
            {
                DeviceType = deviceType;
                Iid = iid;
            }
        }
    }
}