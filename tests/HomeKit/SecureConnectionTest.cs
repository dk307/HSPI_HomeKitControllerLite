using HomeKit;
using HomeKit.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class SecureConnectionTest
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public SecureConnectionTest()
        {
            cancellationTokenSource.CancelAfter(120 * 1000);
        }

        [TestMethod]
        public async Task AccessoryValue()
        {
            using HapAccessory hapAccessory = CreateTemperaturePairedAccessory();
            var connection = await StartTemperatureAccessoryAsync().ConfigureAwait(false);

            var accessoryData = connection.DeviceInfo;

            Assert.AreEqual(1, accessoryData.Accessories.Count);
            Assert.AreEqual("default", accessoryData.Accessories[0].SerialNumber);
            Assert.AreEqual("Sensor1", accessoryData.Accessories[0].Name);

            Assert.AreEqual(2, accessoryData.Accessories[0].Services.Count);

            Assert.AreEqual(Resource.TemperatureSensorPairedAccessoryJson,
                            JsonConvert.SerializeObject(accessoryData));
        }

        [TestMethod]
        public async Task RemovePairing()
        {
            using HapAccessory hapAccessory = CreateTemperaturePairedAccessory();
            var connection = await StartTemperatureAccessoryAsync().ConfigureAwait(false);
            await connection.RemovePairing(cancellationTokenSource.Token).ConfigureAwait(false);
        }

        private async Task<SecureConnection> StartTemperatureAccessoryAsync()
        {
            string controllerFile = Path.Combine("scripts", "temperaturesensor_controller.txt");
            var controllerFileData = File.ReadAllText(controllerFile, Encoding.UTF8);

            var pairingInfo = JsonConvert.DeserializeObject<PairingDeviceInfo>(controllerFileData);
            var connection = new SecureConnection(pairingInfo);

            await connection.ConnectAndListen(cancellationTokenSource.Token).ConfigureAwait(false);
            return connection;
        }

        private static HapAccessory CreateTemperaturePairedAccessory()
        {
            int port = 50001;
            string address = "127.0.0.1";
            string fileName = Path.Combine("scripts", "temperaturesensor_accessory.txt");
            string fileName2 = Path.Combine("scripts", "temperaturesensor_accessory2.txt");

            File.Copy(fileName, fileName2, true);

            string args = $"{port} {address} {fileName2}";
            var hapAccessory = new HapAccessory("temperature_sensor_paried.py", args);
            return hapAccessory;
        }
    }
}