using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class AddFeatureTest
    {
        public AddFeatureTest()
        {
            cancellationTokenSource.CancelAfter(60 * 1000);
        }

        [TestMethod]
        public async Task FeatureAddedOnStart()
        {
            using var hapAccessory = await TestHelper.CreateTemperaturePairedAccessory(CancellationToken.None)
                                                     .ConfigureAwait(false);

            var plugIn = TestHelper.CreatePlugInMock();
            var mockHsController =
                TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());

            int featureRefId = 9385;
            int refId = 8475;
            PlugExtraData extraData = hapAccessory.CreateDevicePlugExtraData();

            // Capture create device data
            SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData = new();

            HsDevice device = new(refId);
            device.Changes[EProperty.PlugExtraData] = extraData;
            device.Changes[EProperty.Relationship] = ERelationship.Device;

            mockHsController.Setup(x => x.GetDeviceWithFeaturesByRef(refId))
                            .Returns(device);

            deviceOrFeatureData.Add(refId, device.Changes);

            mockHsController.Setup(x => x.CreateFeatureForDevice(It.IsAny<NewFeatureData>()))
                            .Returns((NewFeatureData r) =>
                            {
                                featureRefId++;
                                deviceOrFeatureData.Add(featureRefId, r.Feature);
                                return featureRefId;
                            });

            TestHelper.SetupEPropertyGetOrSet(mockHsController, deviceOrFeatureData);

            mockHsController.Setup(x => x.GetRefsByInterface(PlugInData.PlugInId, true))
                            .Returns(new List<int>() { refId });

            mockHsController.Setup(x => x.GetDeviceWithFeaturesByRef(refId))
                            .Returns(device);

            Nito.AsyncEx.AsyncManualResetEvent asyncManualResetEvent = new(false);

            mockHsController.Setup(x => x.UpdateFeatureValueByRef(It.IsAny<int>(), 120.2))
                            .Returns((int devOrFeatRef, double value) =>
                            {
                                deviceOrFeatureData[devOrFeatRef][EProperty.Value] = value;
                                asyncManualResetEvent.Set();
                                return true;
                            });

            Assert.IsTrue(plugIn.Object.InitIO());

            await asyncManualResetEvent.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            Assert.AreEqual(3, deviceOrFeatureData.Count);

            // remove as it is different on machines
            ((PlugExtraData)deviceOrFeatureData[refId][EProperty.PlugExtraData]).RemoveNamed("fallback.address");

            string jsonData = JsonConvert.SerializeObject(deviceOrFeatureData, TestHelper.CreateJsonSerializerForHsData());
            Assert.AreEqual(Resource.TemperatureSensorPairedHS3DataJson, jsonData);

            plugIn.Object.ShutdownIO();
        }

        private readonly CancellationTokenSource cancellationTokenSource = new();
    }
}