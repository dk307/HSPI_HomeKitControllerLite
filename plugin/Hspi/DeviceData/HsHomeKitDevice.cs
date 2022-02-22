using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi.Exceptions;
using Newtonsoft.Json;
using System;
using static System.FormattableString;

#nullable enable

namespace Hspi.DeviceData
{
    internal class HsHomeKitDevice
    {
        public HsHomeKitDevice(IHsController controller, int refId)
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
            var stringData = plugInExtra?[tag];
            if (stringData == null)
            {
                throw new HsDeviceInvalidException(Invariant($"{tag} type not found"));
            }

            var typeData = JsonConvert.DeserializeObject<T>(stringData, converters);
            if (typeData == null)
            {
                throw new HsDeviceInvalidException(Invariant($"{tag} not a valid Json value"));
            }

            return typeData;
        }

        protected T GetPlugExtraData<T>(string tag, params JsonConverter[] converters)
        {
            return GetPlugExtraData<T>(HS, RefId, tag, converters);
        }

        protected void UpdateDeviceValue(in double? data)
        {
            if (data.HasValue)
            {
                HS.UpdatePropertyByRef(RefId, EProperty.InvalidValue, false);

                // only this call triggers events
                if (!HS.UpdateFeatureValueByRef(RefId, data.Value))
                {
                    throw new Exception("Failed to update device");
                }
            }
            else
            {
                HS.UpdatePropertyByRef(RefId, EProperty.InvalidValue, true);
            }
        }

        //Extra data Tags
        public const string AidPlugExtraTag = "accessory.aid";

        public const string CToFNeededPlugExtraTag = "c2f.needed";
        public const string DeviceTypePlugExtraTag = "device.type";
        public const string EnabledCharacteristicPlugExtraTag = "enabled.characteristic";
        public const string FallbackAddressPlugExtraTag = "fallback.address";
        public const string PairInfoPlugExtraTag = "pairing.info";
    }
}