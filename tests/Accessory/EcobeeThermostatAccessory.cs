using System.IO;
using System.Text;

namespace HSPI_HomeKitControllerTest
{
    internal sealed class EcobeeThermostatAccessory : HapAccessory
    {
        public EcobeeThermostatAccessory()
            : base("ecobeethermostat")
        {
            string workingDir = GetWorkingDirectory();
        }

        public override int InitialUpdatesExpectedForDefaultEnabledCharacteristics => 55;

        public string GetHsDeviceAndFeaturesAllString() => GetFileData("hsdeviceandfeaturesall.json");
        public string GetHsDeviceAndFeaturesNoneString() => GetFileData("hsdeviceandfeaturesnone.json");
    }
}