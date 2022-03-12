using System.IO;
using System.Text;

namespace HSPI_HomeKitControllerTest
{
    internal sealed class EcobeeThermostatAccessory : HapAccessory
    {
        public EcobeeThermostatAccessory()
            : base(DirName)
        {
            string workingDir = GetWorkingDirectory();
            this.hsDeviceAndFeaturesAll = Path.Combine(workingDir, DirName, "hsdeviceandfeaturesall.json");
            this.hsDeviceAndFeaturesNone = Path.Combine(workingDir, DirName, "hsdeviceandfeaturesnone.json");
        }

        public override int InitialUpdatesExpectedForDefaultEnabledCharacteristics => 55;

        public string GetHsDeviceAndFeaturesAllString() => File.ReadAllText(this.hsDeviceAndFeaturesAll, Encoding.UTF8);
        public string GetHsDeviceAndFeaturesNoneString() => File.ReadAllText(this.hsDeviceAndFeaturesNone, Encoding.UTF8);
        private const string DirName = "ecobeethermostat";
        private readonly string hsDeviceAndFeaturesAll;
        private readonly string hsDeviceAndFeaturesNone;
    }
}