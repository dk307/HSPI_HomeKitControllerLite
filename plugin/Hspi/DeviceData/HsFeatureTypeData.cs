#nullable enable

using static Hspi.DeviceData.HsHomeKitFeatureDevice;

namespace Hspi.DeviceData
{
    internal sealed record HsFeatureTypeData
    {
        public FeatureType Type { get; init; }
        public ulong? Iid { get; init; }

        public HsFeatureTypeData(FeatureType deviceType, ulong? iid = null)
        {
            Type = deviceType;
            Iid = iid;
        }
    }
}