using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi.Exceptions;
using Hspi.Utils;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using static System.FormattableString;

#nullable enable

namespace Hspi.DeviceData
{
    internal abstract class HsHomeKitDevice
    {
        protected HsHomeKitDevice(IHsController controller, int refId)
        {
            HS = controller;
            RefId = refId;
        }

        public string NameForLog => GetNameForLog(HS, RefId);

        public int RefId { get; init; }

        protected IHsController HS { get; init; }

        public static string GetNameForLog(IHsController hsController, int refId)
        {
            try
            {
                return hsController.GetNameByRef(refId);
            }
            catch
            {
                return Invariant($"RefId:{refId}");
            }
        }

        protected static T GetPlugExtraData<T>(IHsController hsController,
                                               int refId,
                                               string tag,
                                               params JsonConverter[] converters)
        {
            if (hsController.GetPropertyByRef(refId, EProperty.PlugExtraData) is not PlugExtraData plugInExtra)
            {
                throw new HsDeviceInvalidException("PlugExtraData is null");
            }
            return GetPlugExtraData<T>(plugInExtra, tag, converters);
        }

        protected static T GetPlugExtraData<T>(PlugExtraData? plugInExtra,
                                               string tag,
                                               params JsonConverter[] converters)
        {
            if (plugInExtra == null ||
                !plugInExtra.ContainsNamed(tag))
            {
                throw new HsDeviceInvalidException(Invariant($"{tag} type not found"));
            }

            var stringData = plugInExtra[tag];
            if (stringData == null)
            {
                throw new HsDeviceInvalidException(Invariant($"{tag} type not found"));
            }

            try
            {
                var typeData = JsonConvert.DeserializeObject<T>(stringData, converters);
                if (typeData == null)
                {
                    throw new HsDeviceInvalidException(Invariant($"{tag} not a valid Json value"));
                }
                return typeData;
            }
            catch (Exception ex) when (!ex.IsCancelException())
            {
                throw new HsDeviceInvalidException(Invariant($"{tag} type not found"), ex);
            }
        }

        protected T GetPlugExtraData<T>(string tag, params JsonConverter[] converters)
        {
            return GetPlugExtraData<T>(HS, RefId, tag, converters);
        }

        protected void UpdateDeviceValue(in double? data)
        {
            if (Log.IsEnabled(LogEventLevel.Information))
            {
                var existingValue = Convert.ToDouble(HS.GetPropertyByRef(RefId, EProperty.Value));

                Log.Write(existingValue != data ? LogEventLevel.Information : LogEventLevel.Debug,
                          "Updated value {value} for the {name}", data, NameForLog);
            }

            if (data.HasValue && !double.IsNaN(data.Value))
            {
                HS.UpdatePropertyByRef(RefId, EProperty.InvalidValue, false);

                // only this call triggers events
                if (!HS.UpdateFeatureValueByRef(RefId, data.Value))
                {
                    throw new InvalidOperationException($"Failed to update device {NameForLog}");
                }
            }
            else
            {
                HS.UpdatePropertyByRef(RefId, EProperty.InvalidValue, true);
            }
        }

        protected void UpdateDeviceValue(string? data)
        {
            if (Log.IsEnabled(LogEventLevel.Information))
            {
                var existingValue = Convert.ToString(HS.GetPropertyByRef(RefId, EProperty.StatusString));

                Log.Write(existingValue != data ? LogEventLevel.Information : LogEventLevel.Debug,
                          "Updated value {value} for the {name}", data, NameForLog);
            }

            if (!HS.UpdateFeatureValueStringByRef(RefId, data ?? string.Empty))
            {
                throw new InvalidOperationException($"Failed to update device {NameForLog}");
            }
            HS.UpdatePropertyByRef(RefId, EProperty.InvalidValue, false);
        }

        protected void UpdatePlugExtraData(string key, string value)
        {
            UpdatePlugExtraData(new KeyValuePair<string, string>(key, value));
        }

        protected void UpdatePlugExtraData(params KeyValuePair<string, string>[] values)
        {
            if (HS.GetPropertyByRef(RefId, EProperty.PlugExtraData) is not PlugExtraData plugInExtra)
            {
                plugInExtra = new PlugExtraData();
            }

            foreach (var pair in values)
            {
                plugInExtra[pair.Key] = pair.Value;
            }
            HS.UpdatePropertyByRef(RefId, EProperty.PlugExtraData, plugInExtra);
        }

        //Extra data Tags
        public const string AidPlugExtraTag = "accessory.aid";

        public const string CachedAccessoryInfoTag = "cached.accessory.info";
        public const string CToFNeededPlugExtraTag = "c2f.needed";
        public const string DeviceTypePlugExtraTag = "device.type";
        public const string EnabledCharacteristicPlugExtraTag = "enabled.characteristic";
        public const string FallbackAddressPlugExtraTag = "fallback.address";
        public const string PairInfoPlugExtraTag = "pairing.info";
    }
}