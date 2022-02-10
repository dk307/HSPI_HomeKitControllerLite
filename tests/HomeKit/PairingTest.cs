using HomeKit;
using HomeKit.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class PairingTest
    {
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public PairingTest()
        {
            cancellationTokenSource.CancelAfter(120 * 1000);
        }

        [TestMethod]
        public async Task DiscoverAndPairing()
        {
            int port = 50001;
            string pin = "233-34-235";
            string fileName = Guid.NewGuid().ToString("N") + ".obj";

            string args = $"{port} {pin} {fileName}";

            using var hapAccessory = new HapAccessory("temperaturesensor.py", args);

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

            var pairing = await InsecureConnection.StartNewPairing(discoveredDevice, 
                                                                   pin, 
                                                                   cancellationTokenSource.Token);

            Assert.IsFalse(pairing.AccessoryPairingId.IsEmpty);
            Assert.IsFalse(pairing.AccessoryPublicKey.IsEmpty);
            Assert.IsFalse(pairing.ControllerDevicePrivateKey.IsEmpty);
            Assert.IsFalse(pairing.ControllerDevicePublicKey.IsEmpty);
        }
    }
}