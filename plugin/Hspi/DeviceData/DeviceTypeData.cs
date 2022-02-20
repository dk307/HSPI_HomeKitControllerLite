#nullable enable

using static Hspi.DeviceData.HsHomeKitDevice;

namespace Hspi.DeviceData
{
    internal sealed record DeviceTypeData
    {
        public DeviceType Type { get; init; }
        public ulong? Iid { get; init; }

        public DeviceTypeData(DeviceType deviceType, ulong? iid = null)
        {
            Type = deviceType;
            Iid = iid;
        }
    }
}