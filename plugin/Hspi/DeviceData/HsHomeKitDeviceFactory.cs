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
using static Hspi.DeviceData.HsHomeKitCharacteristicFeatureDevice;
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
               .AddGraphicForValue(GetImagePath("online"), OnValue, StatusOnline)
               .AddGraphicForValue(GetImagePath("offline"), OffValue, StatusOffline)
               .PrepareForHsDevice(device.Ref);

            return hsController.CreateFeatureForDevice(newFeatureData);
        }

        public static int CreateDevice(IHsController hsController,
                                       PairingDeviceInfo pairingDeviceInfo,
                                       IPEndPoint fallbackAddress,
                                       Accessory accessory)
        {
            //find default enabled characteristics
            static bool validServiceType(Service x) => x.Type != ServiceType.AccessoryInformation &&
                                                       x.Type != ServiceType.ProtocolInformation;
            var defaultCharacteristics =
                accessory.Services.Values.FirstOrDefault(x => x.Primary == true && validServiceType(x))?.Characteristics?.Values ??
                accessory.Services.Values.FirstOrDefault(validServiceType)?.Characteristics?.Values ??
                Array.Empty<Characteristic>();

            //Ignore hidden & unknown
            defaultCharacteristics = defaultCharacteristics
                .Where(x => !x.Permissions.Contains(CharacteristicPermissions.Hidden) && !string.IsNullOrEmpty(x.Type.DisplayName));

            var extraData = CreateRootPlugInExtraData(pairingDeviceInfo,
                                                      fallbackAddress,
                                                      accessory,
                                                      defaultCharacteristics.Select(x => x.Iid));

            string friendlyName = accessory.Name ??
                                  pairingDeviceInfo.DeviceInformation.DisplayName ??
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

        public static int CreateFeature(IHsController hsController,
                                        int refId,
                                        ServiceType serviceType,
                                        Characteristic characteristic)
        {
            var featureFactory = FeatureFactory.CreateFeature(PlugInData.PlugInId)
                                               .WithLocation(PlugInData.PlugInName)
                                               .WithExtraData(CreatePlugInExtraforDeviceType(FeatureType.Characteristics, characteristic.Iid));

            bool readable = characteristic.Permissions.Contains(CharacteristicPermissions.PairedRead);
            bool writable = characteristic.Permissions.Contains(CharacteristicPermissions.PairedWrite);

            if (!writable && !readable)
            {
                Log.Information("Not creating feature as it not readable and writable");
                throw new InvalidOperationException("Not creating feature as it not readable and writable");
            }
            else if (!writable)
            {
                featureFactory = featureFactory.WithMiscFlags(EMiscFlag.StatusOnly);
            }
            else if (!readable)
            {
                featureFactory = featureFactory.WithoutMiscFlags(EMiscFlag.NoStatusDisplay);
            }

            var mapping = HSMappings.Value.Mappings?.FirstOrDefault(x => x.Iid == characteristic.Type.Id);

            featureFactory = SetName(characteristic, featureFactory);
            featureFactory = SetFeatureType(serviceType, featureFactory, mapping);

            var newData = featureFactory.PrepareForHsDevice(refId);

            if (characteristic.Format != CharacteristicFormat.String)
            {
                bool didAdd = AddValidValuesGraphicsAndStatus(hsController,
                                                              newData,
                                                              serviceType,
                                                              characteristic,
                                                              writable,
                                                              readable,
                                                              mapping);
                if (!didAdd)
                {
                    AddRangedGraphicsAndStatus(hsController,
                                               newData,
                                               serviceType,
                                               characteristic,
                                               writable,
                                               readable,
                                               mapping);
                }
            }

            AddUnitSuffix(hsController, newData, characteristic);

            return hsController.CreateFeatureForDevice(newData);
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

        private static void AddRangedGraphicsAndStatus(IHsController hsController,
                                                       NewFeatureData newData,
                                                       ServiceType serviceType,
                                                       Characteristic characteristic,
                                                       bool writable,
                                                       bool readable,
                                                       HSMapping.HSMapping? mapping)
        {
            double minValue = characteristic.MinimumValue ??
                              characteristic.ValidValuesRange?[0] ??
                              double.MinValue;
            double maxValue = characteristic.MaximumValue ??
                              characteristic.ValidValuesRange?[1] ??
                              double.MaxValue;

            var rangeOptions = mapping?.RangeOptions;

            int decimalPlaces = characteristic.DecimalPlaces ?? 0;

            if (readable)
            {
                var rangeIcon = rangeOptions?.Icon;
                StatusGraphic statusGraphic = new(GetImagePath(rangeIcon ?? GetDefaultIcon(hsController, characteristic)),
                                                  minValue,
                                                  maxValue);
                statusGraphic.TargetRange.DecimalPlaces = decimalPlaces;
                AddStatusGraphic(newData, statusGraphic);
            }

            if (writable)
            {
                int controlUse = rangeOptions?.EControlUses?.FirstOrDefault(x => x.ServiceIId == serviceType.Id)?.Value ?? (int)EControlUse.NotSpecified;
                StatusControl statusControl = new(EControlType.TextBoxNumber)
                {
                    ControlUse = (EControlUse)controlUse,
                    TargetRange = new ValueRange(minValue, maxValue),
                    IsRange = true,
                };
                statusControl.TargetRange.DecimalPlaces = decimalPlaces;

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
                bool scaleF = false;
                if (characteristic.Unit == CharacteristicUnit.Celsius)
                {
                    scaleF = IsTemperatureScaleF(hsController);
                    if (scaleF)
                    {
                        suffix = "F";
                        AddPlugExtraValue(data, CToFNeededPlugExtraTag, "1");
                    }
                }

                data.Feature.Add(EProperty.AdditionalStatusData, new List<string?>() { suffix });

                if (data.Feature.TryGetValue(EProperty.StatusGraphics, out var valueG) &&
                    valueG is StatusGraphicCollection graphics &&
                    graphics.Values != null)
                {
                    foreach (var statusGraphic in graphics.Values)
                    {
                        if (scaleF)
                        {
                            ConvertStatusGraphicToF(statusGraphic);
                        }
                        statusGraphic.HasAdditionalData = true;
                        statusGraphic.TargetRange.Suffix = " " + HsFeature.GetAdditionalDataToken(0);
                    }
                }

                if (data.Feature.TryGetValue(EProperty.StatusControls, out var valueS) &&
                    valueS is StatusControlCollection controls &&
                    controls.Values != null)
                {
                    foreach (var statusControl in controls.Values)
                    {
                        if (scaleF)
                        {
                            ConvertStatusControlToF(statusControl);
                        }
                        statusControl.HasAdditionalData = true;
                        statusControl.TargetRange.Suffix = " " + HsFeature.GetAdditionalDataToken(0);
                    }
                }
            }
        }

        private static bool AddValidValuesGraphicsAndStatus(IHsController hsController,
                                                            NewFeatureData newData,
                                                            ServiceType serviceType,
                                                            Characteristic characteristic,
                                                            bool writable,
                                                            bool readable,
                                                            HSMapping.HSMapping? mapping)
        {
            var list = characteristic.ValidValues ??
                      mapping?.ButtonOptions?.Select(x => x.Value) ??
                      (characteristic.IsBooleanFormatType ? new double[] { 0, 1 } : null);

            if (list == null)
            {
                return false;
            }

            foreach (var value in list)
            {
                var buttonMapping = mapping?.ButtonOptions?.FirstOrDefault(x => x.Value == value);

                if (readable)
                {
                    string iconFileName = GetIcon(hsController, buttonMapping, characteristic, value);
                    StatusGraphic statusGraphic = new(GetImagePath(iconFileName),
                                                      value,
                                                      GetButtonText(buttonMapping, characteristic, value));
                    AddStatusGraphic(newData, statusGraphic);
                }

                if (writable)
                {
                    var controlUse = buttonMapping?.EControlUses?.FirstOrDefault(x => x.ServiceIId == serviceType.Id)?.Value;

                    if (characteristic.IsBooleanFormatType && (buttonMapping == null))
                    {
                        controlUse = (int)((value == 0) ? EControlUse.Off : EControlUse.On);
                    }

                    controlUse ??= (int)EControlUse.NotSpecified;
                    StatusControl statusControl = new(EControlType.Button)
                    {
                        ControlUse = (EControlUse)controlUse,
                        Label = GetButtonText(buttonMapping, characteristic, value),
                        TargetValue = value,
                    };
                    AddStatusControl(newData, statusControl);
                }
            }

            return true;
        }

        private static void ConvertStatusControlToF(StatusControl statusControl)
        {
            if (statusControl.IsRange)
            {
                var newTargetRange = new ValueRange(C2FConvert(statusControl.TargetRange.Min, 3),
                                                    C2FConvert(statusControl.TargetRange.Max, 3))
                {
                    DecimalPlaces = statusControl.TargetRange.DecimalPlaces,
                    Offset = statusControl.TargetRange.Offset,
                    Prefix = statusControl.TargetRange.Prefix,
                    Suffix = statusControl.TargetRange.Suffix,
                };
                statusControl.TargetRange = newTargetRange;
            }
            else
            {
                statusControl.TargetValue = C2FConvert(statusControl.TargetValue, 3);
            }
        }

        private static void ConvertStatusGraphicToF(StatusGraphic statusGraphic)
        {
            if (statusGraphic.IsRange)
            {
                var newTargetRange = new ValueRange(C2FConvert(statusGraphic.TargetRange.Min, 3),
                                                    C2FConvert(statusGraphic.TargetRange.Max, 3))
                {
                    DecimalPlaces = statusGraphic.TargetRange.DecimalPlaces,
                    Offset = statusGraphic.TargetRange.Offset,
                    Prefix = statusGraphic.TargetRange.Prefix,
                    Suffix = statusGraphic.TargetRange.Suffix,
                };
                statusGraphic.TargetRange = newTargetRange;
            }
            else
            {
                statusGraphic.Value = C2FConvert(statusGraphic.Value, 3);
            }
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
                                                               Accessory accessory,
                                                               IEnumerable<ulong> enabledCharacteristics)
        {
            var plugExtra = new PlugExtraData();
            plugExtra.AddNamed(PairInfoPlugExtraTag, JsonConvert.SerializeObject(pairingDeviceInfo, Formatting.Indented));
            plugExtra.AddNamed(FallbackAddressPlugExtraTag, JsonConvert.SerializeObject(fallbackAddress, Formatting.Indented, new IPEndPointJsonConverter()));
            plugExtra.AddNamed(AidPlugExtraTag, JsonConvert.SerializeObject(accessory.Aid));
            plugExtra.AddNamed(EnabledCharacteristicPlugExtraTag, JsonConvert.SerializeObject(enabledCharacteristics));
            plugExtra.AddNamed(CachedAccessoryInfoTag, JsonConvert.SerializeObject(accessory));
            return plugExtra;
        }

        private static (EDeviceType, int) DetermineRootDeviceType(DeviceId device)
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

        private static string GetButtonText(ButtonOption? buttonMapping, Characteristic characteristic, double value)
        {
            return buttonMapping?.Name ??
                   (characteristic.IsBooleanFormatType ? GetOnOffText(value) : null) ??
                   value.ToString(CultureInfo.InvariantCulture);

            static string GetOnOffText(double value)
            {
                return (value == 0D ? "Off" : "On");
            }
        }

        private static string GetDefaultIcon(IHsController hsController, Characteristic characteristic)
        {
            var defaultIconName = characteristic.Type.DisplayName?.ToLower().Replace(" ", "");

            if (defaultIconName != null)
            {
                var iconPath = Path.Combine(hsController.GetAppPath(), GetImagePath(defaultIconName));

                if (File.Exists(iconPath))
                {
                    return defaultIconName;
                }
            }

            return DefaultIcon;
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

        private static string GetIcon(IHsController hsController,
                                      ButtonOption? buttonMapping,
                                      Characteristic characteristic,
                                      double value)
        {
            if (buttonMapping != null)
            {
                return buttonMapping?.Icon ?? GetDefaultIcon(hsController, characteristic);
            }

            if (characteristic.IsBooleanFormatType)
            {
                if (value == 0)
                {
                    return OffIcon;
                }
                return OnIcon;
            }
            return GetDefaultIcon(hsController, characteristic);
        }

        private static string GetImagePath(string iconFileName)
        {
            return Path.ChangeExtension(Path.Combine(PlugInData.PlugInId, "images", iconFileName), "png");
        }

        private static bool IsTemperatureScaleF(IHsController hsController)
        {
            return Convert.ToBoolean(hsController.GetINISetting("Settings", "gGlobalTempScaleF", "True").Trim());
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
                                              FeatureFactory featureFactory)
        {
            string name = characteristic.Type.DisplayName ??
                          characteristic.Description ??
                          characteristic.Type.Id.ToString("D");
            return featureFactory.WithName(name);
        }

        private const string DefaultIcon = "default";
        private const string OffIcon = "off";
        private const string OnIcon = "on";

        private static readonly Lazy<HSMappings> HSMappings = new(() =>
                                                                       {
                                                                           string json = Encoding.UTF8.GetString(Resource.HSMappings);
                                                                           return JsonHelper.DeserializeObject<HSMappings>(json);
                                                                       }, true);
    }
}