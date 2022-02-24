using HomeKit;
using HomeKit.Exceptions;
using HomeKit.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class PairingTest
    {
        public PairingTest()
        {
            cancellationTokenSource.CancelAfter(60 * 1000);
        }

        [TestMethod]
        public async Task DiscoverAndPairing()
        {
            int port = 50001;
            string pin = "233-34-235";
            using HapAccessory hapAccessory = TestHelper.CreateTemperatureUnPairedAccessory(port, pin);
            await hapAccessory.WaitForSuccessStart(cancellationTokenSource.Token).ConfigureAwait(false);
            var discoveredDevice = await DiscoverAndVerify(port).ConfigureAwait(false);

            var pairing = await InsecureConnection.StartNewPairing(discoveredDevice,
                                                                   pin,
                                                                   cancellationTokenSource.Token);

            Assert.IsFalse(pairing.AccessoryPairingId.IsEmpty);
            Assert.IsFalse(pairing.AccessoryPublicKey.IsEmpty);
            Assert.IsFalse(pairing.ControllerDevicePrivateKey.IsEmpty);
            Assert.IsFalse(pairing.ControllerDevicePublicKey.IsEmpty);
        }


        [TestMethod]
        public async Task DiscoverAndPairingFailure()
        {
            int port = 50001;
            string pin = "233-34-245";
            string fileName = Guid.NewGuid().ToString("N") + ".obj";

            string args = $"{port} {pin} {fileName}";
            using var hapAccessory = new HapAccessory("temperature_sensor_unparied.py", args);
            await hapAccessory.WaitForSuccessStart(cancellationTokenSource.Token).ConfigureAwait(false); DiscoveredDevice discoveredDevice = await DiscoverAndVerify(port).ConfigureAwait(false);
            await Assert.ThrowsExceptionAsync<PairingException>(() => InsecureConnection.StartNewPairing(discoveredDevice,
                                                     "123-45-687",
                                                     cancellationTokenSource.Token));
        }


        private async Task<DiscoveredDevice> DiscoverAndVerify(int port)
        {
            IList<DiscoveredDevice> discoveredDevices = null;

            do
            {
                discoveredDevices = (await HomeKitDiscover.DiscoverIPs(TimeSpan.FromMilliseconds(200), cancellationTokenSource.Token).ConfigureAwait(false))
                                    .Where(x => x.DisplayName.StartsWith("Sensor1")).ToList();
            } while (discoveredDevices.Count == 0);

            Assert.AreEqual(1, discoveredDevices.Count);
            DiscoveredDevice discoveredDevice = discoveredDevices[0];
            Assert.AreEqual(port, discoveredDevice.Address.Port);
            Assert.AreEqual(DeviceCategory.Sensors, discoveredDevice.CategoryIdentifier);
            return discoveredDevice;
        }

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    }
}