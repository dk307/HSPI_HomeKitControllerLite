using HomeKit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class SecureConnectionTest
    {
        public SecureConnectionTest()
        {
            cancellationTokenSource.CancelAfter(60 * 1000);
        }

        private CancellationToken Token => cancellationTokenSource.Token;

        [TestMethod]
        public async Task AccessoryValueForTemperatureSensor()
        {
            using var hapAccessory = await TestHelper.CreateTemperaturePairedAccessory(cancellationTokenSource.Token).ConfigureAwait(false);
            await AccessoryValue(hapAccessory).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task AccessoryValueForEcobeeThermostat()
        {
            using var hapAccessory = await TestHelper.CreateEcobeeThermostatPairedAccessory(cancellationTokenSource.Token).ConfigureAwait(false);
            await AccessoryValue(hapAccessory).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task CancelTest()
        {
            using var hapAccessory = await TestHelper.CreateTemperaturePairedAccessory(cancellationTokenSource.Token).ConfigureAwait(false);

            var controllerCancellationTokenSource = new CancellationTokenSource();

            var combined = CancellationTokenSource.CreateLinkedTokenSource(Token, controllerCancellationTokenSource.Token);

            using var connection = await StartTemperatureAccessoryAsync(hapAccessory, combined.Token).ConfigureAwait(false);

            // only cancel the controller
            controllerCancellationTokenSource.Cancel();

            Assert.IsFalse(connection.Connected);
        }

        [TestMethod]
        public async Task DisconnectTest()
        {
            var hapAccessory = await TestHelper.CreateTemperaturePairedAccessory(cancellationTokenSource.Token).ConfigureAwait(false);
            using var connection = await StartTemperatureAccessoryAsync(hapAccessory, Token).ConfigureAwait(false);

            hapAccessory.Dispose();

            // it might be some time before client detects the time,
            // so force connection write
            Assert.IsFalse(await connection.Ping(Token));

            Assert.IsFalse(connection.Connected);
            await Assert.ThrowsExceptionAsync<IOException>(() => connection.RemovePairing(Token));
        }

        [TestMethod]
        public async Task RefreshValueEnqueuesOriginalValueOnSubscribe()
        {
            using var hapAccessory = await TestHelper.CreateTemperaturePairedAccessory(CancellationToken.None).ConfigureAwait(false);
            using var connection = await StartTemperatureAccessoryAsync(hapAccessory, Token).ConfigureAwait(false);

            List<AccessoryValueChangedArgs> changedEventQueue = new();
            connection.AccessoryValueChangedEvent += (s, e) => changedEventQueue.Add(e);

            await connection.RefreshValues(null, Token).ConfigureAwait(false);

            Assert.AreEqual(7, changedEventQueue.Count);

            var data = changedEventQueue[6];

            Assert.AreEqual(1UL, data.Aid);
            Assert.AreEqual(11UL, data.Iid);
            Assert.AreEqual(89.0D, data.Value);
        }

        [TestMethod]
        public async Task RemovePairing()
        {
            using var hapAccessory = await TestHelper.CreateTemperaturePairedAccessory(cancellationTokenSource.Token).ConfigureAwait(false);
            using var connection = await StartTemperatureAccessoryAsync(hapAccessory, Token).ConfigureAwait(false);

            await connection.RemovePairing(Token).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task SubscribeAllGetsNewValues()
        {
            using var hapAccessory = await TestHelper.CreateChangingTemperaturePairedAccessory(CancellationToken.None).ConfigureAwait(false);
            using var connection = await StartTemperatureAccessoryAsync(hapAccessory, Token).ConfigureAwait(false);

            AsyncProducerConsumerQueue<AccessoryValueChangedArgs> changedEventQueue = new();
            connection.AccessoryValueChangedEvent += (s, e) => changedEventQueue.Enqueue(e);

            await connection.TrySubscribeAll(Token).ConfigureAwait(false);

            await changedEventQueue.DequeueAsync(Token).ConfigureAwait(false); //original value
            var data = await changedEventQueue.DequeueAsync(Token).ConfigureAwait(false);

            Assert.IsNotNull(data);
            Assert.AreEqual(1UL, data.Aid);
            Assert.AreEqual(11UL, data.Iid);
            Assert.IsNotNull(data.Value);
        }

        private static async Task<SecureConnection> StartTemperatureAccessoryAsync(HapAccessory hapAccessory,
                                                                                   CancellationToken token)
        {
            var pairingInfo = hapAccessory.GetAccessoryParingInfo();
            var connection = new SecureConnection(pairingInfo);

            await connection.ConnectAndListen(new IPEndPoint(IPAddress.Any, 0), token).ConfigureAwait(false);
            Assert.IsTrue(connection.Connected);
            return connection;
        }

        private async Task AccessoryValue(HapAccessory hapAccessory)
        {
            using var connection = await StartTemperatureAccessoryAsync(hapAccessory, Token).ConfigureAwait(false);

            var accessoryData = connection.DeviceReportedInfo;
            var json = JsonConvert.SerializeObject(accessoryData, TestHelper.CreateJsonSerializer());
            Assert.AreEqual(hapAccessory.GetAccessoryDeviceDataString(), json);
        }

        private readonly CancellationTokenSource cancellationTokenSource = new();
    }
}