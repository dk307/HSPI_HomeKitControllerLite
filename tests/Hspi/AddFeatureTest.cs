using HomeKit.Model;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        public async Task FeatureAddedThroughDevicePage()

        {
            using var hapAccessory = await TestHelper.CreateEcobeeThermostatPairedAccessory(CancellationToken.None).ConfigureAwait(false);
            AsyncProducerConsumerQueue<bool> connectionStatus = new();
            var (plugIn, deviceOrFeatureData) = await StartPluginWithHapAccessory(hapAccessory, connectionStatus);

            var refIds = deviceOrFeatureData.Keys.ToArray();

            string devicePage = plugIn.Object.GetJuiDeviceConfigPage(refIds[0]);

            Assert.IsNotNull(devicePage);

            var page = Page.FromJsonString(devicePage);

            Assert.IsNotNull(page);

            var accessory = JsonConvert.DeserializeObject<DeviceReportedInfo>(hapAccessory.GetAccessoryDeviceDataString()).Accessories.First(x => x.Aid == 1);


            var changes = PageFactory.CreateDeviceConfigPage(page.Id, page.Name);
            //verify all charactertistics are present and toggle off ones
            foreach (var i in accessory.Services.SelectMany(x => x.Value.Characteristics).Select(x => x.Key))
            {
                string viewId = "id_char_" + i.ToString(CultureInfo.InvariantCulture);
                Assert.IsTrue(page.ContainsViewWithId(viewId), $"{viewId} not found");

                ToggleView toggleView = page.GetViewById<ToggleView>(viewId);
                if (!toggleView.IsEnabled)
                {
                    toggleView.IsEnabled = true;
                    changes = changes.WithView(toggleView);
                }
            }

            Assert.IsTrue(page.ContainsViewWithId("PollingTimeSpan"));
            Assert.IsTrue(page.ContainsViewWithId("EnableKeepAliveForConnection"));

            plugIn.Object.SaveJuiDeviceConfigPage(changes.Page.ToJsonString(), refIds[0]);

            // Assert restart of device
            Assert.IsFalse(await connectionStatus.DequeueAsync(cancellationTokenSource.Token).ConfigureAwait(false));
            Assert.IsTrue(await connectionStatus.DequeueAsync(cancellationTokenSource.Token).ConfigureAwait(false));





            plugIn.Object.ShutdownIO();
        }

        [TestMethod]
        public async Task FeatureAddedOnStartForEcobeeThermostat()
        {
            using var hapAccessory = await TestHelper.CreateEcobeeThermostatPairedAccessory(cancellationTokenSource.Token).ConfigureAwait(false);
            await FeatureAddedOnStart(hapAccessory).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task FeatureAddedOnStartForTemperatureSensor()
        {
            using var hapAccessory = await TestHelper.CreateTemperaturePairedAccessory(cancellationTokenSource.Token).ConfigureAwait(false);
            await FeatureAddedOnStart(hapAccessory).ConfigureAwait(false);
        }

        private async Task FeatureAddedOnStart(HapAccessory hapAccessory)
        {
            var plugIn = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());

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

                if (count == (hapAccessory.InitialUpdatesExpectedForDefaultEnabledCharacteristics))
                {
                    asyncManualResetEvent.Set();
                }
            }

            TestHelper.SetupEPropertyGetOrSet(mockHsController, deviceOrFeatureData, updateValueCallback);

            Assert.IsTrue(plugIn.Object.InitIO());

            await asyncManualResetEvent.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            // -1 for root
            Assert.AreEqual(hapAccessory.ExpectedDeviceCreates, deviceOrFeatureData.Count - 1);

            // remove as it is different on machines
            ((PlugExtraData)deviceOrFeatureData[device.Ref][EProperty.PlugExtraData]).RemoveNamed("fallback.address");

            string jsonData = JsonConvert.SerializeObject(deviceOrFeatureData, TestHelper.CreateJsonSerializer());
            Assert.AreEqual(hapAccessory.GetHsDeviceAndFeaturesString(), jsonData);

            plugIn.Object.ShutdownIO();
        }

        private async Task<(Mock<PlugIn>, SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData)>
            StartPluginWithHapAccessory(HapAccessory hapAccessory, AsyncProducerConsumerQueue<bool> connectionQueue)
        {
            Mock<PlugIn> plugIn;
            string hsData = hapAccessory.GetHsDeviceAndFeaturesString();

            int[] refIds = null;
            void updateValueCallback(int devOrFeatRef, EProperty property, object value)
            {
                if (refIds[1] == devOrFeatRef &&
                    property == EProperty.Value)
                {
                    connectionQueue.Enqueue((double)value != 0);
                }
            }

            TestHelper.SetupHsDataForSyncing(hsData,
                                             updateValueCallback,
                                             out plugIn,
                                             out Mock<IHsController> mockHsController,
                                             out SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData);

            refIds = deviceOrFeatureData.Keys.ToArray();

            Assert.IsTrue(plugIn.Object.InitIO());

            Assert.IsTrue(await connectionQueue.DequeueAsync(cancellationTokenSource.Token).ConfigureAwait(false));

            int featureRefId = refIds.Max();
            mockHsController.Setup(x => x.CreateFeatureForDevice(It.IsAny<NewFeatureData>()))
                            .Returns((NewFeatureData r) =>
                            {
                                featureRefId++;
                                deviceOrFeatureData.Add(featureRefId, r.Feature);
                                return featureRefId;
                            });

            mockHsController.Setup(x => x.DeleteFeature(It.IsAny<int>()))
                            .Callback((int featureRefId) =>
                            {
                                deviceOrFeatureData.Remove(featureRefId);
                            });

            return (plugIn, deviceOrFeatureData);
        }

        private readonly CancellationTokenSource cancellationTokenSource = new();
    }
}