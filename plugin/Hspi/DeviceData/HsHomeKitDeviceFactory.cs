using HomeKit.Model;
using HomeKit.Utils;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi.DeviceData.HSMapping;
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
using static Hspi.DeviceData.HsHomeKitConnectedFeatureDevice;
using static Hspi.DeviceData.HsHomeKitDevice;
using static Hspi.DeviceData.HsHomeKitFeatureDevice;
using static System.FormattableString;

#nullable enable

namespace Hspi.DeviceData
{
    internal static class HsHomeKitDeviceFactory
    {
        public static int CreateAndUpdateConnectedFeature(IHsController hsController,
                                                   HsDevice device)
        {
            foreach (var feature in device.Features)
            {
                if (GetDeviceTypeFromPlugInData(feature.PlugExtraData)?.Type == FeatureType.OnlineStatus)
                {
                    return feature.Ref;
                }
            }

            var newFeatureData = FeatureFactory.CreateFeature(PlugInData.PlugInId)
               .WithName("Connected")
               .WithLocation(PlugInData.PlugInName)
               .WithMiscFlags(EMiscFlag.StatusOnly)
               .AsType(EFeatureType.Generic, 0)
               .WithExtraData(CreatePlugInExtraforDeviceType(FeatureType.OnlineStatus))
               .AddGraphicForValue(CreateImagePath("online"), OnValue, StatusOnline)
               .AddGraphicForValue(CreateImagePath("offline"), OffValue, StatusOffline)
               .PrepareForHsDevice(device.Ref);

            return hsController.CreateFeatureForDevice(newFeatureData);
        }

        public static int CreateFeature(IHsController hsController,
                                        int refId,
                                        ServiceType serviceType,
                                        Characteristic characteristic)
        {
            var featureFactory = FeatureFactory.CreateFeature(PlugInData.PlugInId)
                                               .WithLocation(PlugInData.PlugInName)
                                               .WithExtraData(CreatePlugInExtraforDeviceType(FeatureType.Characteristics, characteristic.Iid));

            bool writable = characteristic.Permissions.Contains(CharacteristicPermissions.PairedWrite);

            if (!writable)
            {
                featureFactory = featureFactory.WithMiscFlags(EMiscFlag.StatusOnly);
            }

            var mapping = HSMappings.Value.Mappings?.FirstOrDefault(x => x.Iid == characteristic.Type.Id);

            featureFactory = SetName(characteristic, featureFactory, mapping);
            featureFactory = SetFeatureType(serviceType, featureFactory, mapping);

            var newData = featureFactory.PrepareForHsDevice(refId);

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

            AddUnitSuffix(hsController, newData, characteristic);

            return hsController.CreateFeatureForDevice(newData);
        }

        public static int CreateHsDevice(IHsController hsController,
                                                         PairingDeviceInfo pairingDeviceInfo,
                                         IPEndPoint fallbackAddress,
                                         Accessory accessory)
        {
            //find default enabled characteristics
            var defaultCharacteristics =
                accessory.Services.Values.FirstOrDefault(x => x.Primary == true)?.Characteristics?.Values ??
                accessory.Services.Values.FirstOrDefault(x => x.Type != ServiceType.AccessoryInformation &&
                                                              x.Type != ServiceType.ProtocolInformation)?.Characteristics?.Values ??
                Array.Empty<Characteristic>();

            var extraData = CreateRootPlugInExtraData(pairingDeviceInfo,
                                                      fallbackAddress,
                                                      accessory.Aid,
                                                      defaultCharacteristics.Select(x => x.Iid));

            string friendlyName = pairingDeviceInfo.DeviceInformation.DisplayName ??
                                  accessory.Name ??
                                  pairingDeviceInfo.DeviceInformation.Model ??
                                  Invariant($"HomeKit Device - {pairingDeviceInfo.DeviceInformation.Id}");

            var (deviceType, subFeatureType) = DetermineRootDeviceType(pairingDeviceInfo.DeviceInformation);
            var newDeviceData = DeviceFactory.CreateDevice(PlugInData.PlugInId)
                                             .WithName(friendlyName)
                                             .AsType(deviceType, subFeatureType)
                                             .WithLocation(PlugInData.PlugInName)
                                             .WithMiscFlags(EMiscFlag.SetDoesNotChangeLastChange)
                                             .WithExtraData(extraData)
                                             .PrepareForHs();

            int refId = hsController.CreateDevice(newDeviceData);
            Log.Information("Created device {friendlyName}", friendlyName);
            return refId;
        }
        private static void AddPlugExtraValue(NewFeatureData data,
                                                  string key,
                                          string value)
        {
            if (data.Feature[EProperty.PlugExtraData] is not PlugExtraData plugExtraData)
            {
                plugExtraData = new PlugExtraData();
            }
            plugExtraData.AddNamed(key, value);
            data.Feature[EProperty.PlugExtraData] = plugExtraData;
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

            StatusGraphic statusGraphic = new(CreateImagePath(rangeIcon ?? DefaultIcon),
                                              minValue,
                                              maxValue);
            int decimalPlaces = (characteristic.StepValue.HasValue ?
                    GetPrecision((decimal)characteristic.StepValue.Value) : 0);

            statusGraphic.TargetRange.DecimalPlaces = decimalPlaces;

            AddStatusGraphic(newData, statusGraphic);

            if (writable)
            {
                int controlUse = rangeOptions?.EControlUses?.FirstOrDefault(x => x.ServiceIId == serviceType.Id)?.Value ?? (int)EControlUse.NotSpecified;
                StatusControl statusControl = new(EControlType.TextBoxNumber)
                {
                    ControlUse = (EControlUse)controlUse,
                    TargetRange = new ValueRange(minValue, maxValue),
                };

                AddStatusControl(newData, statusControl);
            }
        }

        private static void AddStatusControl(NewFeatureData newData, StatusControl statusControl)
        {
            if (!newData.Feature.TryGetValue(EProperty.StatusControls, out var value))
            {
                value = new StatusControlCollection();
                newData.Feature[EProperty.StatusControls] = value;
            }

            var statusControls = (StatusControlCollection)value;
            statusControls.Add(statusControl);
        }

        private static void AddStatusGraphic(NewFeatureData newData, StatusGraphic statusGraphic)
        {
            if (!newData.Feature.TryGetValue(EProperty.StatusGraphics, out var value))
            {
                value = new StatusGraphicCollection();
                newData.Feature[EProperty.StatusGraphics] = value;
            }

            var statusGraphics = (StatusGraphicCollection)value;
            statusGraphics.Add(statusGraphic);
        }

        private static void AddUnitSuffix(IHsController hsController,
                                          NewFeatureData data,
                                          Characteristic characteristic)
        {
            if (characteristic.Unit != null)
            {
                var unitAttribute = EnumHelper.GetAttribute<UnitAttribute>(characteristic.Unit);
                var suffix = unitAttribute?.Unit;
                if (characteristic.Unit == CharacteristicUnit.Celsius)
                {
                    var scaleF = Convert.ToBoolean(hsController.GetINISetting("Settings", "gGlobalTempScaleF", "True").Trim());
                    if (scaleF)
                    {
                        suffix = "F";
                        AddPlugExtraValue(data, CToFNeededPlugExtraTag, "1");
                    }
                }

                if (!string.IsNullOrWhiteSpace(suffix))
                {
                    data.Feature.Add(EProperty.AdditionalStatusData, new List<string>() { suffix! });
                    if (data.Feature[EProperty.StatusGraphics] is StatusGraphicCollection graphics &&
                        graphics.Values != null)
                    {
                        foreach (var statusGraphic in graphics.Values)
                        {
                            statusGraphic.HasAdditionalData = true;

                            if (statusGraphic.IsRange)
                            {
                                statusGraphic.TargetRange.Suffix = " " + HsFeature.GetAdditionalDataToken(0);
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
                var buttonMapping = mapping?.ButtonOptions?.FirstOrDefault(x => x.Value == value);
                StatusGraphic statusGraphic = new(CreateImagePath(buttonMapping?.Icon ?? DefaultIcon),
                                                  value,
                                                  buttonMapping?.Name ?? value.ToString(CultureInfo.InvariantCulture));
                AddStatusGraphic(newData, statusGraphic);

                if (writable)
                {
                    int controlUse = buttonMapping?.EControlUses?.FirstOrDefault(x => x.ServiceIId == serviceType.Id)?.Value ?? (int)EControlUse.NotSpecified;
                    StatusControl statusControl = new(EControlType.Button)
                    {
                        ControlUse = (EControlUse)controlUse,
                        Label = buttonMapping?.Name ?? value.ToString(CultureInfo.InvariantCulture),
                        TargetValue = value,
                    };
                    AddStatusControl(newData, statusControl);
                }
            }
        }

        private static string CreateImagePath(string featureName)
        {
            return Path.ChangeExtension(Path.Combine(PlugInData.PlugInId, "images", featureName), "png");
        }

        private static PlugExtraData CreatePlugInExtraforDeviceType(FeatureType featureType,
                                                                    ulong? iid = null)
        {
            var plugExtra = new PlugExtraData();
            HsFeatureTypeData value = new(featureType, iid);
            plugExtra.AddNamed(DeviceTypePlugExtraTag,
                               JsonConvert.SerializeObject(value));
            return plugExtra;
        }

        private static PlugExtraData CreateRootPlugInExtraData(PairingDeviceInfo pairingDeviceInfo,
                                                               IPEndPoint fallbackAddress,
                                                               ulong aid,
                                                               IEnumerable<ulong> enabledCharacteristics)
        {
            var plugExtra = new PlugExtraData();
            plugExtra.AddNamed(PairInfoPlugExtraTag, JsonConvert.SerializeObject(pairingDeviceInfo, Formatting.Indented));
            plugExtra.AddNamed(FallbackAddressPlugExtraTag, JsonConvert.SerializeObject(fallbackAddress, Formatting.Indented, new IPEndPointJsonConverter()));
            plugExtra.AddNamed(AidPlugExtraTag, JsonConvert.SerializeObject(aid));
            plugExtra.AddNamed(EnabledCharacteristicPlugExtraTag, JsonConvert.SerializeObject(enabledCharacteristics));
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

        private static HsFeatureTypeData? GetDeviceTypeFromPlugInData(PlugExtraData? plugInExtra)
        {
            if (plugInExtra != null && plugInExtra.NamedKeys.Contains(DeviceTypePlugExtraTag))
            {
                var data = plugInExtra[DeviceTypePlugExtraTag];
                return JsonConvert.DeserializeObject<HsFeatureTypeData>(data);
            }

            return null;
        }

        private static int GetPrecision(decimal x)
        {
            int precision = 0;
            while (x * (decimal)Math.Pow(10, precision) != Math.Round(x * (decimal)Math.Pow(10, precision)))
            {
                precision++;
            }
            return precision;
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

        private const string DefaultIcon = "default.png";
        private static readonly Lazy<HSMappings> HSMappings = new(() =>
                                                                          {
                                                                              string json = Encoding.UTF8.GetString(Resource.HSMappings);
                                                                              return JsonHelper.DeserializeObject<HSMappings>(json);
                                                                          }, true);
    }
}