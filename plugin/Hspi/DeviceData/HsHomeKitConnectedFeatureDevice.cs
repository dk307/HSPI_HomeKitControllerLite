using HomeSeer.PluginSdk;

#nullable enable

namespace Hspi.DeviceData
{
    internal class HsHomeKitConnectedFeatureDevice : HsHomeKitFeatureDevice
    {
        public HsHomeKitConnectedFeatureDevice(IHsController controller, int refId)
            : base(controller, refId)
        {
        }
    }
}