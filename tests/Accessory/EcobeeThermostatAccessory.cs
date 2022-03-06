namespace HSPI_HomeKitControllerTest
{
    internal sealed class EcobeeThermostatAccessory : HapAccessory
    {
        public EcobeeThermostatAccessory() 
            : base(DirName)
        {
        }

        public override int ExpctedDeviceCreates => 3;
        public override int InitialUpdatesExpected => 5;

        private const string DirName = "ecobeethermostat";
    }
}