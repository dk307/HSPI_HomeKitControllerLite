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

        public FeatureTypeData GetTypeData()
        {
            var typeData = GetPlugExtraData<FeatureTypeData>(DeviceTypePlugExtraTag);

            if ((typeData.Type == FeatureType.Characteristics) && (typeData.Iid == null))
            {
                throw new HsDeviceInvalidException(Invariant($"Device Type data not valid for {RefId}"));
            }

            return typeData;
        }

        public static FeatureTypeData GetTypeData(PlugExtraData plugExtraData)
        {
            var typeData = GetPlugExtraData<FeatureTypeData>(plugExtraData, DeviceTypePlugExtraTag);

            if ((typeData.Type == FeatureType.Characteristics) && (typeData.Iid == null))
            {
                throw new HsDeviceInvalidException(Invariant($"Device Type data not valid"));
            }

            return typeData;
        }
    }
}