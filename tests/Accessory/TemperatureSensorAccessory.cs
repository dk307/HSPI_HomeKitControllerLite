namespace HSPI_HomeKitControllerTest
{
    internal sealed class TemperatureSensorAccessory : HapAccessory
    {
        public TemperatureSensorAccessory(bool changing = false) 
            : base("temperatureSensor", changing ? "temperaturesensor_changing" : null)
        {
        }

        public override int InitialUpdatesExpectedForDefaultEnabledCharacteristics => 5;
    }
}