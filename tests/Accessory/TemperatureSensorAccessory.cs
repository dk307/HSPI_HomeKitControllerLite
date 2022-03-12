namespace HSPI_HomeKitControllerTest
{
    internal sealed class TemperatureSensorAccessory : HapAccessory
    {
        public TemperatureSensorAccessory(bool changing = false) 
            : base(DirName, changing ? "temperaturesensor_changing" : null)
        {
        }

        public override int InitialUpdatesExpectedForDefaultEnabledCharacteristics => 5;
        private const string DirName = "temperatureSensor";
    }
}