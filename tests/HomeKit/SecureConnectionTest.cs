﻿using HomeKit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Nito.AsyncEx;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class SecureConnectionTest
    {
        public SecureConnectionTest()
        {
            cancellationTokenSource.CancelAfter(120 * 1000);
        }

        private CancellationToken Token => cancellationTokenSource.Token;

        [TestMethod]
        public async Task AccessoryValue()
        {
            using HapAccessory hapAccessory = TestHelper.CreateTemperaturePairedAccessory();
            await hapAccessory.WaitForSuccessStart(Token).ConfigureAwait(false);
            using var connection = await StartTemperatureAccessoryAsync(Token).ConfigureAwait(false);

            var accessoryData = connection.DeviceReportedInfo;

            Assert.AreEqual(1, accessoryData.Accessories.Count);
            Assert.AreEqual("default", accessoryData.Accessories[0].SerialNumber);
            Assert.AreEqual("Sensor1", accessoryData.Accessories[0].Name);

            Assert.AreEqual(2, accessoryData.Accessories[0].Services.Count);

            Assert.AreEqual(Resource.TemperatureSensorPairedAccessoryJson,
                            JsonConvert.SerializeObject(accessoryData));
        }

        [TestMethod]
        public async Task CancelTest()
        {
            using var hapAccessory = TestHelper.CreateTemperaturePairedAccessory();
            await hapAccessory.WaitForSuccessStart(Token).ConfigureAwait(false);

            var controllerCancellationTokenSource = new CancellationTokenSource();

            var combined = CancellationTokenSource.CreateLinkedTokenSource(Token, controllerCancellationTokenSource.Token);

            using var connection = await StartTemperatureAccessoryAsync(combined.Token).ConfigureAwait(false);

            // only cancel the controller
            controllerCancellationTokenSource.Cancel();

            Assert.IsFalse(connection.Connected);
        }

        [TestMethod]
        public async Task DisconnectTest()
        {
            var hapAccessory = TestHelper.CreateTemperaturePairedAccessory();
            await hapAccessory.WaitForSuccessStart(Token).ConfigureAwait(false);
            using var connection = await StartTemperatureAccessoryAsync(Token).ConfigureAwait(false);

            hapAccessory.Dispose();

            // it might be some time before client detects the time, so force connection
            Assert.IsFalse(await connection.Ping(Token));

            Assert.IsFalse(connection.Connected);
            await Assert.ThrowsExceptionAsync<IOException>(() => connection.RemovePairing(Token));
        }

        [TestMethod]
        public async Task RemovePairing()
        {
            using HapAccessory hapAccessory = TestHelper.CreateTemperaturePairedAccessory();
            await hapAccessory.WaitForSuccessStart(Token).ConfigureAwait(false);
            using var connection = await StartTemperatureAccessoryAsync(Token).ConfigureAwait(false);

            await connection.RemovePairing(Token).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task SubscribeAllEnqueuesOriginalValueOnSubscribe()
        {
            using HapAccessory hapAccessory = TestHelper.CreateTemperaturePairedAccessory();
            await hapAccessory.WaitForSuccessStart(Token).ConfigureAwait(false);
            using var connection = await StartTemperatureAccessoryAsync(Token).ConfigureAwait(false);

            AsyncProducerConsumerQueue<ChangedEvent> changedEventQueue = new();

            await connection.TrySubscribeAll(changedEventQueue, Token).ConfigureAwait(false);

            var data = await changedEventQueue.DequeueAsync(Token) as AccessoryValueChangedEvent;

            Assert.AreEqual(1UL, data.Aid);
            Assert.AreEqual(9UL, data.Iid);
            Assert.AreEqual(49.0D, data.Value);
        }

        [TestMethod]
        public async Task SubscribeAllGetsNewValues()
        {
            using HapAccessory hapAccessory = TestHelper.CreateTemperaturePairedAccessory("temperature_sensor_paried_changing.py");
            await hapAccessory.WaitForSuccessStart(Token).ConfigureAwait(false);
            using var connection = await StartTemperatureAccessoryAsync(Token).ConfigureAwait(false);

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

        private static async Task<SecureConnection>
            StartTemperatureAccessoryAsync(CancellationToken token)
        {
            var pairingInfo = TestHelper.GetTemperatureSensorParingInfo();
            var connection = new SecureConnection(pairingInfo);

            await connection.ConnectAndListen(token).ConfigureAwait(false);
            Assert.IsTrue(connection.Connected);
            return connection;
        }

        private readonly CancellationTokenSource cancellationTokenSource = new();
    }
}