using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi.Exceptions;
using Newtonsoft.Json;
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

        public string Name => HS.GetNameByRef(RefId);

        public int RefId { get; init; }

        protected IHsController HS { get; init; }

        protected T GetPlugExtraData<T>(string tag, params JsonConverter[] converters)
        {
            return GetPlugExtraData<T>(HS, RefId, tag, converters);
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

        //Extra data Tags
        public const string AidPlugExtraTag = "Accessory.Aid";

        public const string CToFNeededPlugExtraTag = "C2F.needed";
        public const string DeviceTypePlugExtraTag = "Device.Type";
        public const string FallbackAddressPlugExtraTag = "Fallback.Address";
        public const string PairInfoPlugExtraTag = "Pairing.Info";
        public const string EnabledCharacteristicPlugExtraTag = "Enabled.Characteristic";
    }
}