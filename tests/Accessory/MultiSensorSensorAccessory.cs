namespace HSPI_HomeKitControllerTest
{
    internal sealed class MultiSensorSensorAccessory : HapAccessory
    {
        public MultiSensorSensorAccessory() 
            : base("multisensor")
        {
        }

        public override int InitialUpdatesExpectedForDefaultEnabledCharacteristics => 1;

        public string GetSecondaryDeviceNewDataString() => GetFileData("secondarydevicenewdata.json");

    }
}