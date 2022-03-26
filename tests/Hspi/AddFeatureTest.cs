using HomeKit.Model;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using System;
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

        [TestMethod]
        public async Task FeatureAddedThroughDevicePage()
        {
            using var hapAccessory = await TestHelper.CreateEcobeeThermostatPairedAccessory(CancellationToken.None).ConfigureAwait(false);

            string expectedHsAndDeviceData = hapAccessory.GetHsDeviceAndFeaturesAllString();

            var jsonData = await TestEnabledFeatureChanged(hapAccessory, AddFeatures).ConfigureAwait(false);
            Assert.AreEqual(expectedHsAndDeviceData, jsonData);

            static PageFactory AddFeatures(Page page, Accessory accessory)
            {
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

                return changes;
            }
        }

        [DataTestMethod]
        [DataRow("999", true)]
        [DataRow("", false)]
        public async Task ChangeOptionsThroughDevicePage(string pollingTimeSpan,
                                                         bool enableKeepAliveForConnection)
        {
            using var hapAccessory = await TestHelper.CreateChangingTemperaturePairedAccessory(CancellationToken.None).ConfigureAwait(false);

            var jsonData = await TestEnabledFeatureChanged(hapAccessory, ChangeOptions).ConfigureAwait(false);

            var data = JsonConvert.DeserializeObject<SortedDictionary<int, Dictionary<EProperty, object>>>(jsonData,
                                                                                                           TestHelper.CreateJsonSerializer());

            var plugExtraData = (JArray)data[HapAccessory.StartDeviceRefId][EProperty.PlugExtraData];

            var pairingDeviceInfo = JsonConvert.DeserializeObject<PairingDeviceInfo>((string)plugExtraData.First(x => (string)x["key"] == "pairing.info")["value"]);

            Assert.AreEqual(enableKeepAliveForConnection, pairingDeviceInfo.EnableKeepAliveForConnection);
            Assert.AreEqual(string.IsNullOrWhiteSpace(pollingTimeSpan) ? null : TimeSpan.FromSeconds(int.Parse(pollingTimeSpan)), 
                            pairingDeviceInfo.PollingTimeSpan);

            PageFactory ChangeOptions(Page page, Accessory accessory)
            {
                var changes = PageFactory.CreateDeviceConfigPage(page.Id, page.Name);

                var pollingTimeSpanView = page.GetViewById<InputView>("PollingTimeSpan");
                var enableKeepAliveForConnectionView = page.GetViewById<ToggleView>("EnableKeepAliveForConnection");
                pollingTimeSpanView.UpdateValue(pollingTimeSpan);
                enableKeepAliveForConnectionView.IsEnabled = enableKeepAliveForConnection;

                changes = changes.WithView(pollingTimeSpanView);
                changes = changes.WithView(enableKeepAliveForConnectionView);

                return changes;
            }
        }

        [TestMethod]
        public async Task FeatureDeletedThroughDevicePage()
        {
            using var hapAccessory = await TestHelper.CreateEcobeeThermostatPairedAccessory(CancellationToken.None).ConfigureAwait(false);
            string expectedHsAndDeviceData = hapAccessory.GetHsDeviceAndFeaturesNoneString();

            var jsonData = await TestEnabledFeatureChanged(hapAccessory, RemoveFeatures).ConfigureAwait(false);
            Assert.AreEqual(expectedHsAndDeviceData, jsonData);

            static PageFactory RemoveFeatures(Page page, Accessory accessory)
            {
                var changes = PageFactory.CreateDeviceConfigPage(page.Id, page.Name);

                //verify all charactertistics are present and toggle off ones
                foreach (var i in accessory.Services.SelectMany(x => x.Value.Characteristics).Select(x => x.Key))
                {
                    string viewId = "id_char_" + i.ToString(CultureInfo.InvariantCulture);
                    Assert.IsTrue(page.ContainsViewWithId(viewId), $"{viewId} not found");

                    ToggleView toggleView = page.GetViewById<ToggleView>(viewId);
                    if (toggleView.IsEnabled)
                    {
                        toggleView.IsEnabled = false;
                        changes = changes.WithView(toggleView);
                    }
                }

                return changes;
            }
        }

        private static string GetHsDeviceAndFeatureData(int refId,
                                                        SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData)
        {
            // remove as it is different on machines
            ((PlugExtraData)deviceOrFeatureData[refId][EProperty.PlugExtraData]).RemoveNamed("fallback.address");

            string jsonData = JsonConvert.SerializeObject(deviceOrFeatureData, TestHelper.CreateJsonSerializer());
            return jsonData;
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

            string jsonData = GetHsDeviceAndFeatureData(device.Ref, deviceOrFeatureData);
            Assert.AreEqual(hapAccessory.GetHsDeviceAndFeaturesString(), jsonData);

            plugIn.Object.ShutdownIO();
        }

        private async Task<string> TestEnabledFeatureChanged(HapAccessory hapAccessory,
                                                             Func<Page, Accessory, PageFactory> generateChanges)
        {
            AsyncProducerConsumerQueue<bool> connectionStatus = new();
            var (plugIn, deviceOrFeatureData) = await TestHelper.StartPluginWithHapAccessory(hapAccessory, 
                                                                                             connectionStatus,
                                                                                             cancellationTokenSource.Token);

            var refIds = deviceOrFeatureData.Keys.ToArray();

            string devicePage = plugIn.Object.GetJuiDeviceConfigPage(refIds[0]);

            Assert.IsNotNull(devicePage);

            var page = Page.FromJsonString(devicePage);

            Assert.IsNotNull(page);

            var accessory = JsonConvert.DeserializeObject<DeviceReportedInfo>(hapAccessory.GetAccessoryDeviceDataString()).Accessories.First(x => x.Aid == 1);

            PageFactory changes = generateChanges(page, accessory);

            Assert.IsTrue(page.ContainsViewWithId("PollingTimeSpan"));
            Assert.IsTrue(page.ContainsViewWithId("EnableKeepAliveForConnection"));

            plugIn.Object.SaveJuiDeviceConfigPage(changes.Page.ToJsonString(), refIds[0]);

            // Assert restart of device
            Assert.IsFalse(await connectionStatus.DequeueAsync(cancellationTokenSource.Token).ConfigureAwait(false));
            Assert.IsTrue(await connectionStatus.DequeueAsync(cancellationTokenSource.Token).ConfigureAwait(false));

            string jsonData = GetHsDeviceAndFeatureData(refIds[0], deviceOrFeatureData);

            plugIn.Object.ShutdownIO();
            return jsonData;
        }

        private readonly CancellationTokenSource cancellationTokenSource = new();
    }
}