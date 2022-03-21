using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class UnpairDeviceTest
    {
        public UnpairDeviceTest()
        {
            cancellationTokenSource.CancelAfter(60 * 1000);
        }

        [TestMethod]
        public async Task UnpairAlreadyConnected()
        {
            using var hapAccessory =
                await TestHelper.CreateTemperaturePairedAccessory(cancellationTokenSource.Token).ConfigureAwait(false);
            AsyncProducerConsumerQueue<bool> connectionStatus = new();
            var (plugIn, deviceOrFeatureData) = await TestHelper.StartPluginWithHapAccessory(hapAccessory,
                                                                                             connectionStatus,
                                                                                             cancellationTokenSource.Token);

            JObject pairRequest = new();
            pairRequest.Add("action", new JValue("unpair"));
            pairRequest.Add("data", new JValue(HapAccessory.StartDeviceRefId));

            string data2 = plugIn.Object.PostBackProc("UnpairDevice.html", pairRequest.ToString(), string.Empty, 0);

            var result2 = JsonConvert.DeserializeObject<JObject>(data2);

            Assert.IsNotNull(result2);
            Assert.IsNull((string)result2["ErrorMessage"]);

            Assert.IsFalse(await connectionStatus.DequeueAsync(cancellationTokenSource.Token).ConfigureAwait(false));

            plugIn.Object.ShutdownIO();
        }

        private readonly CancellationTokenSource cancellationTokenSource = new();
    }
}