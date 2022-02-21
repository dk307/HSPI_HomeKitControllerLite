using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi.Exceptions;
using static System.FormattableString;

#nullable enable

namespace Hspi.DeviceData
{
    internal class HsHomeKitFeatureDevice : HsHomeKitDevice
    {
        public HsHomeKitFeatureDevice(IHsController controller, int refId) 
            : base(controller, refId)
        {
        }

        public enum FeatureType
        {
            OnlineStatus = 1,
            Characteristics = 2
        }

        public bool GetCToFNeeded()
        {
            var plugInExtra = HS.GetPropertyByRef(RefId, EProperty.PlugExtraData) as PlugExtraData;
            var stringData = plugInExtra?[CToFNeededPlugExtraTag];
            return (stringData != null);
        }

        public HsFeatureTypeData GetTypeData()
        {
            var typeData = GetPlugExtraData<HsFeatureTypeData>(DeviceTypePlugExtraTag);

            if ((typeData.Type == FeatureType.Characteristics) && (typeData.Iid == null))
            {
                throw new HsDeviceInvalidException(Invariant($"Device Type data not valid for {RefId}"));
            }

            return typeData;
        }

        public static HsFeatureTypeData GetTypeData(PlugExtraData plugExtraData)
        {
            var typeData = GetPlugExtraData<HsFeatureTypeData>(plugExtraData, DeviceTypePlugExtraTag);

            if ((typeData.Type == FeatureType.Characteristics) && (typeData.Iid == null))
            {
                throw new HsDeviceInvalidException(Invariant($"Device Type data not valid"));
            }

            return typeData;
        }
    }
}