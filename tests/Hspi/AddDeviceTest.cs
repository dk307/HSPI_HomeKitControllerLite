using HomeKit.Model;
using HomeKit.Utils;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class AddDeviceTest
    {
        public AddDeviceTest()
        {
            cancellationTokenSource.CancelAfter(60 * 1000);
        }

        [TestMethod]
        public async Task AddDevice()
        {
            string pin = "133-34-295";
            using var hapAccessory = await TestHelper.CreateTemperatureUnPairedAccessory(pin, cancellationTokenSource.Token)
                                                     .ConfigureAwait(false);

            var plugIn = TestHelper.CreatePlugInMock();
            var hsControllerMock =
                TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());

            // Capture create device data
            NewDeviceData newDataForDevice = null;
            hsControllerMock.Setup(x => x.CreateDevice(It.IsAny<NewDeviceData>()))
                            .Callback<NewDeviceData>(r => newDataForDevice = r)
                            .Returns(1);

            Assert.IsTrue(plugIn.Object.InitIO());

            //discover
            string data = plugIn.Object.PostBackProc("AddDevice.html", "{\"action\":\"search\"}", string.Empty, 0);

            var result = JsonConvert.DeserializeObject<JObject>(data);

            Assert.IsNotNull(result);
            Assert.IsNull((string)result["ErrorMessage"]);
            Assert.AreEqual(1, (result["Data"] as JArray).Count);

            JObject pairRequest = new();

            pairRequest.Add("action", new JValue("pair"));
            pairRequest.Add("pincode", new JValue(pin));
            pairRequest.Add("data", (result["Data"] as JArray)[0]);

            //add
            string data2 = plugIn.Object.PostBackProc("AddDevice.html", pairRequest.ToString(), string.Empty, 0);

            var result2 = JsonConvert.DeserializeObject<JObject>(data2);

            Assert.IsNotNull(result2);
            Assert.IsNull((string)result2["ErrorMessage"]);

            Assert.IsNotNull(newDataForDevice);
            Assert.IsTrue(((string)newDataForDevice.Device[EProperty.Name]).StartsWith("Sensor"));
            Assert.AreEqual(ERelationship.Device, newDataForDevice.Device[EProperty.Relationship]);
            Assert.AreEqual(PlugInData.PlugInName, newDataForDevice.Device[EProperty.Location]);
            Assert.AreEqual((int)EDeviceType.Generic, ((TypeInfo)newDataForDevice.Device[EProperty.DeviceType]).Type);
            Assert.AreEqual(2, ((TypeInfo)newDataForDevice.Device[EProperty.DeviceType]).SubType);

            var extraData = (PlugExtraData)newDataForDevice.Device[EProperty.PlugExtraData];
            Assert.IsNotNull(JsonConvert.DeserializeObject<PairingDeviceInfo>(extraData["pairing.info"]));
            Assert.AreEqual(1UL, JsonConvert.DeserializeObject<ulong>(extraData["accessory.aid"]));
            Assert.IsNotNull(JsonConvert.DeserializeObject<IPEndPoint>(extraData["fallback.address"], new IPEndPointJsonConverter()));
            CollectionAssert.AreEqual(new ulong[] { 11 },
                                      JsonConvert.DeserializeObject<ulong[]>(extraData["enabled.characteristic"]));
            plugIn.Object.ShutdownIO();
        }

        [TestMethod]
        public async Task AddDeviceIgnoresAlreadyPairedOnesAsync()
        {
            using var hapAccessory = await TestHelper.CreateTemperaturePairedAccessory(cancellationTokenSource.Token)
                                                     .ConfigureAwait(false);

            var plugIn = TestHelper.CreatePlugInMock();
            TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());
            Assert.IsTrue(plugIn.Object.InitIO());

            string data = plugIn.Object.PostBackProc("AddDevice.html", "{\"action\":\"search\"}", string.Empty, 0);
            Assert.AreEqual("{\"ErrorMessage\":null,\"Data\":[]}", data);
            plugIn.Object.ShutdownIO();
        }

        [TestMethod]
        public void AddDeviceTestWithNoAccessory()
        {
            var plugIn = TestHelper.CreatePlugInMock();
            TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());
            Assert.IsTrue(plugIn.Object.InitIO());

            string data = plugIn.Object.PostBackProc("AddDevice.html", "{\"action\":\"search\"}", string.Empty, 0);
            Assert.AreEqual("{\"ErrorMessage\":null,\"Data\":[]}", data);
            plugIn.Object.ShutdownIO();
        }

        [TestMethod]
        public async Task AddDeviceWithAuthFailure()
        {
            string pin = "133-34-295";
            using var hapAccessory = await TestHelper.CreateTemperatureUnPairedAccessory(pin, cancellationTokenSource.Token)
                                                     .ConfigureAwait(false);

            var plugIn = TestHelper.CreatePlugInMock();
            TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());

            Assert.IsTrue(plugIn.Object.InitIO());

            //discover
            string data = plugIn.Object.PostBackProc("AddDevice.html", "{\"action\":\"search\"}", string.Empty, 0);

            var result = JsonConvert.DeserializeObject<JObject>(data);

            Assert.IsNotNull(result);
            Assert.IsNull((string)result["ErrorMessage"]);
            Assert.AreEqual(1, (result["Data"] as JArray).Count);

            JObject pairRequest = new();

            pairRequest.Add("action", new JValue("pair"));
            pairRequest.Add("pincode", new JValue("345-34-345"));
            pairRequest.Add("data", (result["Data"] as JArray)[0]);

            //add
            string data2 = plugIn.Object.PostBackProc("AddDevice.html", pairRequest.ToString(), string.Empty, 0);
            var result2 = JsonConvert.DeserializeObject<JObject>(data2);

            Assert.IsNotNull(result2);
            Assert.IsNotNull((string)result2["ErrorMessage"]);

            plugIn.Object.ShutdownIO();
        }

        private readonly CancellationTokenSource cancellationTokenSource = new();
    }
}