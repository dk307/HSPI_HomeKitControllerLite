using HomeSeer.PluginSdk.Devices;
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

            HsDevice device = hapAccessory.SetDeviceRefExpectations(mockHsController);

            // Capture create device data
            SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData = new();
            deviceOrFeatureData.Add(device.Ref, device.Changes);

            int featureRefId = HapAccessory.StartFeatureRefId;
            mockHsController.Setup(x => x.CreateFeatureForDevice(It.IsAny<NewFeatureData>()))
                            .Returns((NewFeatureData r) =>
                            {
                                featureRefId++;
                                deviceOrFeatureData.Add(featureRefId, r.Feature);
                                return featureRefId;
                            });

            Nito.AsyncEx.AsyncManualResetEvent asyncManualResetEvent = new(false);

            int count = 0;
            void updateValueCallback(int a, EProperty n, object w)
            {
                count++;

                if (count == (hapAccessory.InitialUpdatesExpected))
                {
                    asyncManualResetEvent.Set();
                }
            }

            TestHelper.SetupEPropertyGetOrSet(mockHsController, deviceOrFeatureData, updateValueCallback);

            Assert.IsTrue(plugIn.Object.InitIO());

            await asyncManualResetEvent.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            Assert.AreEqual(hapAccessory.ExpctedDeviceCreates, deviceOrFeatureData.Count);

            // remove as it is different on machines
            ((PlugExtraData)deviceOrFeatureData[device.Ref][EProperty.PlugExtraData]).RemoveNamed("fallback.address");

            string jsonData = JsonConvert.SerializeObject(deviceOrFeatureData, TestHelper.CreateJsonSerializerForHsData());
            Assert.AreEqual(hapAccessory.GetHsDeviceAndFeaturesString(), jsonData);

            plugIn.Object.ShutdownIO();
        }

        private readonly CancellationTokenSource cancellationTokenSource = new();
    }
}