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

        public void SetConnectedState(bool connected)
        {
            UpdateDeviceValue(connected ? OnValue : OffValue);
        }

        public const double OffValue = 0;
        public const double OnValue = 1;
        public const string StatusOffline = "Offline";
        public const string StatusOnline = "Online";
    }
}