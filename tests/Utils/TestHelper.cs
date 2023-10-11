using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Logging;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    internal static class TestHelper
    {
        public static async Task<TemperatureSensorAccessory>
            CreateChangingTemperaturePairedAccessory(CancellationToken token)
        {
            var hapAccessory = new TemperatureSensorAccessory(true);
            await hapAccessory.StartPaired(token).ConfigureAwait(false);
            return hapAccessory;
        }

        public static async Task<EcobeeThermostatAccessory>
            CreateEcobeeThermostatPairedAccessory(CancellationToken token)
        {
            var hapAccessory = new EcobeeThermostatAccessory();
            await hapAccessory.StartPaired(token).ConfigureAwait(false);
            return hapAccessory;
        }

        public static JsonSerializerSettings CreateJsonSerializer()
        {
            return new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,

                Converters = new List<JsonConverter>() { new PlugExtraDataConverter(),
                                                         new StatusGraphicReadConverter(),
                                                         new StatusControlReadConverter()}
            };
        }

        public static (Mock<PlugIn> mockPlugin, Mock<IHsController> mockHsController)
         CreateMockPluginAndHsController(Dictionary<string, string> settingsFromIni)
        {
            var mockPlugin = new Mock<PlugIn>(MockBehavior.Loose)
            {
                CallBase = true,
            };

            var mockHsController = SetupHsControllerAndSettings(mockPlugin, settingsFromIni);

            mockPlugin.Object.InitIO();

            return (mockPlugin, mockHsController);
        }

        public static async Task<MultiSensorSensorAccessory>
            CreateMultiSensorPairedAccessory(CancellationToken token)
        {
            var hapAccessory = new MultiSensorSensorAccessory();
            await hapAccessory.StartPaired(token).ConfigureAwait(false);
            return hapAccessory;
        }

        public static async Task<MultiSensorSensorAccessory> 
            CreateMultiSensorUnpairedAccessory(string pin, CancellationToken token)
        {
            var hapAccessory = new MultiSensorSensorAccessory();
            await hapAccessory.StartUnpaired(pin, token).ConfigureAwait(false);
            return hapAccessory;
        }

        public static Mock<PlugIn> CreatePlugInMock()
        {
            return new Mock<PlugIn>(MockBehavior.Loose)
            {
                CallBase = true,
            };
        }

        public static async Task<TemperatureSensorAccessory>
            CreateTemperaturePairedAccessory(CancellationToken token)
        {
            var hapAccessory = new TemperatureSensorAccessory();
            await hapAccessory.StartPaired(token).ConfigureAwait(false);
            return hapAccessory;
        }

        public static async Task<TemperatureSensorAccessory> CreateTemperatureUnPairedAccessory(string pin,
                                                                                  CancellationToken token)
        {
            var hapAccessory = new TemperatureSensorAccessory();
            await hapAccessory.StartUnpaired(pin, token).ConfigureAwait(false);
            return hapAccessory;
        }

        public static void SetupEPropertyGetOrSet(Mock<IHsController> mockHsController,
                                                  SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData,
                                                  Action<int, EProperty, object> updateValueCallback = null)
        {
            mockHsController.Setup(x => x.GetPropertyByRef(It.IsAny<int>(), It.IsAny<EProperty>()))
                .Returns((int devOrFeatRef, EProperty property) =>
                {
                    deviceOrFeatureData[devOrFeatRef].TryGetValue(property, out object data);
                    return data;
                });

            mockHsController.Setup(x => x.UpdateFeatureValueByRef(It.IsAny<int>(), It.IsAny<double>()))
                .Returns((int devOrFeatRef, double value) =>
                {
                    deviceOrFeatureData[devOrFeatRef][EProperty.Value] = value;
                    updateValueCallback?.Invoke(devOrFeatRef, EProperty.Value, value);
                    return true;
                });

            mockHsController.Setup(x => x.UpdateFeatureValueStringByRef(It.IsAny<int>(), It.IsAny<string>()))
                .Returns((int devOrFeatRef, string value) =>
                {
                    deviceOrFeatureData[devOrFeatRef][EProperty.StatusString] = value;
                    updateValueCallback?.Invoke(devOrFeatRef, EProperty.StatusString, value);
                    return true;
                });

            mockHsController.Setup(x => x.UpdatePropertyByRef(It.IsAny<int>(), It.IsAny<EProperty>(), It.IsAny<object>()))
                .Callback((int devOrFeatRef, EProperty property, object value) =>
                {
                    deviceOrFeatureData[devOrFeatRef][property] = value;
                    updateValueCallback?.Invoke(devOrFeatRef, property, value);
                });
        }

        public static Mock<IHsController> SetupHsControllerAndSettings(Mock<PlugIn> mockPlugin,
                                                                       Dictionary<string, string> settingsFromIni)
        {
            var mockHsController = new Mock<IHsController>(MockBehavior.Strict);

            // set mock homeseer via reflection
            Type plugInType = typeof(AbstractPlugin);
            var method = plugInType.GetMethod("set_HomeSeerSystem", BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance);
            method.Invoke(mockPlugin.Object, new object[] { mockHsController.Object });

            mockHsController.Setup(x => x.GetINISetting("Settings", "gGlobalTempScaleF", "True", "")).Returns("True");
            mockHsController.Setup(x => x.GetIniSection("Settings", PlugInData.PlugInId + ".ini")).Returns(settingsFromIni);
            mockHsController.Setup(x => x.SaveINISetting("Settings", It.IsAny<string>(), It.IsAny<string>(), PlugInData.PlugInId + ".ini"));
            mockHsController.Setup(x => x.WriteLog(It.IsAny<ELogType>(), It.IsAny<string>(), PlugInData.PlugInName, It.IsAny<string>()));
            mockHsController.Setup(x => x.RegisterDeviceIncPage(PlugInData.PlugInId, It.IsAny<string>(), It.IsAny<string>()));
            mockHsController.Setup(x => x.RegisterFeaturePage(PlugInData.PlugInId, It.IsAny<string>(), It.IsAny<string>()));
            mockHsController.Setup(x => x.GetRefsByInterface(PlugInData.PlugInId, true)).Returns(new List<int>());
            mockHsController.Setup(x => x.GetNameByRef(It.IsAny<int>())).Returns("Test");
            mockHsController.Setup(x => x.GetAppPath()).Returns(string.Empty);
            return mockHsController;
        }

        public static void SetupHsDataForSyncing(string hsData,
                                          Action<int, EProperty, object> updateValueCallback,
                                          out Mock<PlugIn> plugIn,
                                          out Mock<IHsController> mockHsController,
                                          out SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData)
        {
            plugIn = TestHelper.CreatePlugInMock();
            mockHsController = TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());
            deviceOrFeatureData = JsonConvert.DeserializeObject<
                SortedDictionary<int, Dictionary<EProperty, object>>>(hsData, TestHelper.CreateJsonSerializer());

            foreach (var changes in deviceOrFeatureData)
            {
                JArray jArray = (JArray)changes.Value[EProperty.PlugExtraData];

                PlugExtraData extraData = new();
                foreach (var token in jArray)
                {
                    JObject jObject = (JObject)token;
                    extraData.AddNamed((string)jObject["key"], (string)jObject["value"]);
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

            TestHelper.SetupEPropertyGetOrSet(mockHsController, deviceOrFeatureData, updateValueCallback);

            mockHsController.Setup(x => x.GetRefsByInterface(PlugInData.PlugInId, true))
                            .Returns(new List<int>() { deviceRefId });
        }

        public static async Task<(Mock<PlugIn>, SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData)>
         StartPluginWithHapAccessory(HapAccessory hapAccessory,
                                     AsyncProducerConsumerQueue<bool> connectionQueue,
                                     CancellationToken cancellationToken)
        {
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
                                             out Mock<PlugIn> plugIn,
                                             out Mock<IHsController> mockHsController,
                                             out SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData);

            refIds = deviceOrFeatureData.Keys.ToArray();

            int featureRefId = refIds.Max();
            mockHsController.Setup(x => x.CreateFeatureForDevice(It.IsAny<NewFeatureData>()))
                            .Returns((NewFeatureData r) =>
                            {
                                featureRefId++;
                                deviceOrFeatureData.Add(featureRefId, r.Feature);
                                return featureRefId;
                            });

            mockHsController.Setup(x => x.DeleteFeature(It.IsAny<int>()))
                            .Returns((int featureRefId) =>
                            {
                                deviceOrFeatureData.Remove(featureRefId);
                                return true;
                            });

            Assert.IsTrue(plugIn.Object.InitIO());

            Assert.IsTrue(await connectionQueue.DequeueAsync(cancellationToken).ConfigureAwait(false));

            return (plugIn, deviceOrFeatureData);
        }

        public static async Task WaitTillSameAsync(string expected, Func<string> valueFtn, CancellationToken token)
        {
            while (!token.IsCancellationRequested && expected != valueFtn())
            {
                await Task.Delay(50, token).ConfigureAwait(false);
            }
            Assert.AreEqual(expected, valueFtn());
        }

        public static void VerifyHtmlValid(string html)
        {
            HtmlAgilityPack.HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(html);
            Assert.AreEqual(0, htmlDocument.ParseErrors.Count());
        }
    }
}