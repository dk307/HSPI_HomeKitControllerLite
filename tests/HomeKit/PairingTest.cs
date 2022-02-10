using HomeKit;
using HomeKit.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class PairingTest
    {
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public PairingTest()
        {
            cancellationTokenSource.CancelAfter(60 * 1000);
        }

        [TestMethod]
        public async Task Discover()
        {
            int port = 50001;
            string pin = "233-34-235";
            string fileName = Guid.NewGuid().ToString("N") + ".obj";

            string args = $"{port} {pin} {fileName}";

            using var hapAccessory = new HapAccessory("temperaturesensor.py", args);

            IList<DiscoveredDevice> discoveredDevices = null;

            do
            {
                discoveredDevices = await HomeKitDiscover.DiscoverIPs(TimeSpan.FromMilliseconds(200), cancellationTokenSource.Token);
            } while (discoveredDevices.Count == 0);

            Assert.AreEqual(1, discoveredDevices.Count);
            DiscoveredDevice discoveredDevice = discoveredDevices[0];
            Assert.AreEqual(DeviceStatus.NotPaired, discoveredDevice.Status);
            Assert.AreEqual(50001, discoveredDevice.Address.Port);
            Assert.AreEqual(DeviceCategory.Sensors, discoveredDevice.CategoryIdentifier);
            Assert.IsTrue(discoveredDevice.DisplayName.StartsWith("Sensor1"));
        }
    }
}