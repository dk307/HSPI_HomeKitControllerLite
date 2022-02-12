using HomeKit;
using HomeKit.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Nito.AsyncEx;
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
        private CancellationToken Token => cancellationTokenSource.Token;

        public SecureConnectionTest()
        {
            cancellationTokenSource.CancelAfter(120 * 1000);
        }

        [TestMethod]
        public async Task AccessoryValue()
        {
            using HapAccessory hapAccessory = CreateTemperaturePairedAccessory();
            await hapAccessory.WaitForSuccessStart(Token).ConfigureAwait(false);
            using var connection = await StartTemperatureAccessoryAsync().ConfigureAwait(false);

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
            await hapAccessory.WaitForSuccessStart(Token).ConfigureAwait(false);
            using var connection = await StartTemperatureAccessoryAsync().ConfigureAwait(false);

            await connection.RemovePairing(Token).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task SubscribeAllEnqueuesOriginalValueOnSubscribe()
        {
            using HapAccessory hapAccessory = CreateTemperaturePairedAccessory();
            await hapAccessory.WaitForSuccessStart(Token).ConfigureAwait(false);
            using var connection = await StartTemperatureAccessoryAsync().ConfigureAwait(false);

            AsyncProducerConsumerQueue<ChangedEvent> changedEventQueue = new();

            await connection.TrySubscribeAll(changedEventQueue, Token).ConfigureAwait(false);

            var data = await changedEventQueue.DequeueAsync(Token) as AccessoryValueChangedEvent;

            Assert.AreEqual(1UL, data.Aid);
            Assert.AreEqual(9UL, data.Iid);
            Assert.AreEqual("49", data.Value);
        }

        [TestMethod]
        public async Task SubscribeAllGetsNewValues()
        {
            using HapAccessory hapAccessory = CreateTemperaturePairedAccessory("temperature_sensor_paried_changing.py");
            await hapAccessory.WaitForSuccessStart(Token).ConfigureAwait(false);
            using var connection = await StartTemperatureAccessoryAsync().ConfigureAwait(false);

            AsyncProducerConsumerQueue<ChangedEvent> changedEventQueue = new();

            await connection.TrySubscribeAll(changedEventQueue, Token).ConfigureAwait(false);

            await changedEventQueue.DequeueAsync(Token).ConfigureAwait(false); //original value
            var eventC = await changedEventQueue.DequeueAsync(Token).ConfigureAwait(false);

            var data = eventC as AccessoryValueChangedEvent;

            Assert.IsNotNull(data);
            Assert.AreEqual(1UL, data.Aid);
            Assert.AreEqual(9UL, data.Iid);
            Assert.IsNotNull(data.Value);
        }

        private async Task<SecureConnection> StartTemperatureAccessoryAsync()
        {
            string controllerFile = Path.Combine("scripts", "temperaturesensor_controller.txt");
            var controllerFileData = File.ReadAllText(controllerFile, Encoding.UTF8);

            var pairingInfo = JsonConvert.DeserializeObject<PairingDeviceInfo>(controllerFileData);
            var connection = new SecureConnection(pairingInfo);

            await connection.ConnectAndListen(Token).ConfigureAwait(false);
            Assert.IsTrue(connection.Connected);
            return connection;
        }

        private static HapAccessory CreateTemperaturePairedAccessory(
                            string script = "temperature_sensor_paried.py")
        {
            int port = 50001;
            string address = "0.0.0.0";
            string fileName = Path.Combine("scripts", "temperaturesensor_accessory.txt");
            string fileName2 = Path.Combine("scripts", "temperaturesensor_accessory2.txt");

            File.Copy(fileName, fileName2, true);

            string args = $"{port} {address} {fileName2}";
            var hapAccessory = new HapAccessory(script, args);
            return hapAccessory;
        }
    }
}