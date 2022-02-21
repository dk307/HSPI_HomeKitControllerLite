using HomeSeer.PluginSdk;

#nullable enable

namespace Hspi.DeviceData
{
    internal class HsHomeKitCharacteristicFeatureDevice : HsHomeKitFeatureDevice
    {
        public HsHomeKitCharacteristicFeatureDevice(IHsController controller, int refId) 
            : base(controller, refId)
        {
        }
    }
}