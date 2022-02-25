using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using Hspi.DeviceData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class DeviceWorkingTest
    {
        public DeviceWorkingTest()
        {
            cancellationTokenSource.CancelAfter(60 * 1000);
        }

        [TestMethod]
        public async Task ConnectedUpdate()
        {
            using var hapAccessory = TestHelper.CreateTemperaturePairedAccessory("temperature_sensor_paried_changing.py");
            await hapAccessory.WaitForSuccessStart(cancellationTokenSource.Token).ConfigureAwait(false);
            string hsData = Resource.TemperatureSensorPairedHS3DataJson;

            Mock<PlugIn> plugIn;
            Mock<IHsController> mockHsController;
            SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData;
            SetupHsDataForSyncing(hsData, out plugIn, out mockHsController, out deviceOrFeatureData);

            Nito.AsyncEx.AsyncManualResetEvent asyncManualResetEvent = new(false);

            mockHsController.Setup(x => x.UpdateFeatureValueByRef(It.IsAny<int>(), 140))
                            .Returns((int devOrFeatRef, double value) =>
                            {
                                deviceOrFeatureData[devOrFeatRef][EProperty.Value] = value;
                                asyncManualResetEvent.Set();
                                return true;
                            });

            Assert.IsTrue(plugIn.Object.InitIO());

            await asyncManualResetEvent.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            var refIds = deviceOrFeatureData.Keys.ToArray();
            var plugExtraData = ((PlugExtraData)deviceOrFeatureData[refIds[0]][EProperty.PlugExtraData]);
            Assert.IsFalse(plugExtraData[HsHomeKitDevice.FallbackAddressPlugExtraTag].Contains("0.0.0.0"));

            Assert.AreEqual(1D, deviceOrFeatureData[refIds[1]][EProperty.Value]);
            Assert.AreEqual(140D, deviceOrFeatureData[refIds[1]][EProperty.Value]);

            plugIn.Object.ShutdownIO();
        }

        private static void SetupHsDataForSyncing(string hsData,
                                                   out Mock<PlugIn> plugIn,
                                                   out Mock<IHsController> mockHsController,
                                                   out SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData)
        {
            plugIn = TestHelper.CreatePlugInMock();
            mockHsController = TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());
            deviceOrFeatureData = JsonConvert.DeserializeObject<
                SortedDictionary<int, Dictionary<EProperty, object>>>(hsData,
                TestHelper.CreateJsonSerializerForHsData());

            var deviceRefId = deviceOrFeatureData.Keys.First();

            ((PlugExtraData)deviceOrFeatureData[deviceRefId][EProperty.PlugExtraData])
                .AddNamed("fallback.address", "{\"Address\": \"0.0.0.0\",\"Port\": \"8473\"}");

            HsDevice device = new(deviceRefId);

            foreach (var pair in deviceOrFeatureData)
            {
                if (pair.Key == deviceRefId)
                {
                    foreach (var x in pair.Value)
                    {
                        device.Changes.Add(x.Key, x.Value);
                    }
                }
                else
                {
                    HsFeature feature = new(pair.Key);
                    foreach (var x in pair.Value)
                    {
                        feature.Changes.Add(x.Key, x.Value);
                    }
                    device.Features.Add(feature);
                }
            }

            mockHsController.Setup(x => x.GetDeviceWithFeaturesByRef(deviceRefId))
                            .Returns(device);

            TestHelper.SetupEPropertyGetOrSet(mockHsController, deviceOrFeatureData);

            mockHsController.Setup(x => x.GetRefsByInterface(PlugInData.PlugInId, true))
                            .Returns(new List<int>() { deviceRefId });

            mockHsController.Setup(x => x.GetDeviceWithFeaturesByRef(deviceRefId))
                            .Returns(device);
        }

        private readonly CancellationTokenSource cancellationTokenSource = new();
    }
}