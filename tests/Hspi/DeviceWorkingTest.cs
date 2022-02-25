using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using Hspi.DeviceData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        public async Task ConnectionWorking()
        {
            using var hapAccessory = TestHelper.CreateTemperaturePairedAccessory("temperature_sensor_paried_changing.py");
            await hapAccessory.WaitForSuccessStart(cancellationTokenSource.Token).ConfigureAwait(false);
            string hsData = Resource.TemperatureSensorPairedHS3DataJson;

            SetupHsDataForSyncing(hsData,
                                  out Mock<PlugIn> plugIn,
                                  out Mock<IHsController> mockHsController,
                                  out SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData);

            Nito.AsyncEx.AsyncManualResetEvent asyncManualResetEvent = new(false);

            int count = 0;
            mockHsController.Setup(x => x.UpdateFeatureValueByRef(deviceOrFeatureData.Keys.Last(), It.IsAny<double>()))
                            .Returns((int devOrFeatRef, double value) =>
                            {
                                deviceOrFeatureData[devOrFeatRef][EProperty.Value] = value;
                                if (count == 3)
                                {
                                    asyncManualResetEvent.Set();
                                }
                                count++;
                                return true;
                            });

            Assert.IsTrue(plugIn.Object.InitIO());

            await asyncManualResetEvent.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            var refIds = deviceOrFeatureData.Keys.ToArray();
            var plugExtraData = ((PlugExtraData)deviceOrFeatureData[refIds[0]][EProperty.PlugExtraData]);
            Assert.IsFalse(plugExtraData[HsHomeKitDevice.FallbackAddressPlugExtraTag].Contains("0.0.0.0"));

            Assert.AreEqual(1D, deviceOrFeatureData[refIds[1]][EProperty.Value]);

            plugIn.Object.ShutdownIO();
        }

        [TestMethod]
        public async Task ConnectionReconnect()
        {
            var hapAccessory1 = TestHelper.CreateTemperaturePairedAccessory("temperature_sensor_paried_changing.py");
            await hapAccessory1.WaitForSuccessStart(cancellationTokenSource.Token).ConfigureAwait(false);
            string hsData = Resource.TemperatureSensorPairedHS3DataJson;

            SetupHsDataForSyncing(hsData,
                                  out Mock<PlugIn> plugIn,
                                  out Mock<IHsController> mockHsController,
                                  out SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData);

            Nito.AsyncEx.AsyncManualResetEvent onlineEvent = new(false);
            Nito.AsyncEx.AsyncManualResetEvent onlineEvent2 = new(false);
            Nito.AsyncEx.AsyncManualResetEvent offlineEvent = new(false);

            var refIds = deviceOrFeatureData.Keys.ToArray();

            mockHsController.Setup(x => x.UpdateFeatureValueByRef(refIds[1], It.IsAny<double>()))
                            .Returns((int devOrFeatRef, double value) =>
                            {
                                deviceOrFeatureData[devOrFeatRef][EProperty.Value] = value;
                                if (value == 1)
                                {
                                    if (!onlineEvent.IsSet)
                                    {
                                        onlineEvent.Set();
                                    }
                                    else
                                    {
                                        onlineEvent2.Set();
                                    }
                                }
                                else
                                {
                                    offlineEvent.Set();
                                }
                                return true;
                            });

            Assert.IsTrue(plugIn.Object.InitIO());

            await onlineEvent.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            Assert.AreEqual(1D, deviceOrFeatureData[refIds[1]][EProperty.Value]);

            hapAccessory1.Dispose();

            await offlineEvent.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            Assert.AreEqual(0D, deviceOrFeatureData[refIds[1]][EProperty.Value]);

            //Restart accessory
            var hapAccessory2 = TestHelper.CreateTemperaturePairedAccessory("temperature_sensor_paried_changing.py");
            await hapAccessory2.WaitForSuccessStart(cancellationTokenSource.Token).ConfigureAwait(false);

            await onlineEvent2.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            Assert.AreEqual(1D, deviceOrFeatureData[refIds[1]][EProperty.Value]);

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

            foreach (var changes in deviceOrFeatureData)
            {
                JArray jArray = (JArray)changes.Value[EProperty.PlugExtraData];

                PlugExtraData extraData = new();
                foreach (var token in jArray)
                {
                    JObject jObject = (JObject)token;
                    extraData.AddNamed((string)jObject["key"], (string?)jObject["value"]);
                }
                changes.Value[EProperty.PlugExtraData] = extraData;
            }

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