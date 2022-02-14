using HomeKit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nito.AsyncEx;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class SecureConnectionManagerTest
    {
        public SecureConnectionManagerTest()
        {
            cancellationTokenSource.CancelAfter(120 * 1000);
        }

        [TestMethod]
        public async Task SimpleConnectionAndCancel()
        {
            using var hapAccessory = TestHelper.CreateTemperaturePairedAccessory();
            await hapAccessory.WaitForSuccessStart(Token).ConfigureAwait(false);

            var pairingInfo = TestHelper.GetTemperatureSensorParingInfo();
            AsyncProducerConsumerQueue<ChangedEvent> changedEventQueue = new();
            var manager = new SecureConnectionManager();
            manager.ConnectAndListenDevice(pairingInfo, changedEventQueue, Token);

            //not connected
            var notConnected = (await changedEventQueue.DequeueAsync(Token).ConfigureAwait(false)) as DeviceConnectionChangedEvent;
            Assert.IsFalse(notConnected.Connected);

            // Initial value
            _ = (await changedEventQueue.DequeueAsync(Token).ConfigureAwait(false));

            //connected
            var connected = (await changedEventQueue.DequeueAsync(Token).ConfigureAwait(false)) as DeviceConnectionChangedEvent;
            Assert.IsTrue(connected.Connected);

            Assert.IsTrue(manager.Connection.Connected);

            Assert.IsTrue(await manager.Connection.Ping(Token));

            cancellationTokenSource.Cancel();

            Assert.IsFalse(manager.Connection.Connected);
        }

        [TestMethod]
        public async Task ReconnectionAfterDisconnect()
        {
            var hapAccessory1 = TestHelper.CreateTemperaturePairedAccessory();
            await hapAccessory1.WaitForSuccessStart(Token).ConfigureAwait(false);

            var pairingInfo = TestHelper.GetTemperatureSensorParingInfo();
            AsyncProducerConsumerQueue<ChangedEvent> changedEventQueue = new();
            var manager = new SecureConnectionManager();
            manager.ConnectAndListenDevice(pairingInfo, changedEventQueue, Token);

            //Consume initial events
            _ = (await changedEventQueue.DequeueAsync(Token).ConfigureAwait(false));
            _ = (await changedEventQueue.DequeueAsync(Token).ConfigureAwait(false));
            _ = (await changedEventQueue.DequeueAsync(Token).ConfigureAwait(false));

            hapAccessory1.Dispose();

            // it might be some time before client detects the time, so force connection
            Assert.IsFalse(await manager.Connection.Ping(Token));

            Assert.IsFalse(manager.Connection.Connected);

            using var hapAccessory2 = TestHelper.CreateTemperaturePairedAccessory();
            await hapAccessory2.WaitForSuccessStart(Token).ConfigureAwait(false);

            //not connected
            var notConnected = (await changedEventQueue.DequeueAsync(Token).ConfigureAwait(false)) as DeviceConnectionChangedEvent;
            Assert.IsFalse(notConnected.Connected);

            // Initial value
            _ = (await changedEventQueue.DequeueAsync(Token).ConfigureAwait(false));
            _ = (await changedEventQueue.DequeueAsync(Token).ConfigureAwait(false));

            //connected
            var connected = (await changedEventQueue.DequeueAsync(Token).ConfigureAwait(false)) as DeviceConnectionChangedEvent;
            Assert.IsTrue(connected.Connected);

            Assert.IsTrue(manager.Connection.Connected);
            cancellationTokenSource.Cancel();
        }

        private CancellationToken Token => cancellationTokenSource.Token;

        private readonly CancellationTokenSource cancellationTokenSource = new();
    }
}