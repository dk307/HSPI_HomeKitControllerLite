namespace HSPI_HomeKitControllerTest
{
    internal sealed class EcobeeThermostatAccessory : HapAccessory
    {
        public EcobeeThermostatAccessory()
            : base(DirName)
        {
        }

        public override int InitialUpdatesExpected => 55;

        private const string DirName = "ecobeethermostat";
    }
}