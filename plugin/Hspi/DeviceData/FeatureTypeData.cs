#nullable enable

using static Hspi.DeviceData.HsHomeKitFeatureDevice;

namespace Hspi.DeviceData
{
    internal sealed record FeatureTypeData
    {
        public FeatureType Type { get; init; }
        public ulong? Iid { get; init; }

        public FeatureTypeData(FeatureType deviceType, ulong? iid = null)
        {
            Type = deviceType;
            Iid = iid;
        }
    }
}