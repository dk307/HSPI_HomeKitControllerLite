using HomeKit.Model;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Logging;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    internal static class TestHelper
    {
        public static JsonSerializerSettings CreateJsonSerializerForHsData()
        {
            return new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                Converters = new List<JsonConverter>() { new PlugExtraDataConverter(),
                                                         new StatusGraphicReadConverter() }
            };
        }

        public static Mock<PlugIn> CreatePlugInMock()
        {
            return new Mock<PlugIn>(MockBehavior.Loose)
            {
                CallBase = true,
            };
        }

        public static async Task<TemperatureSensorAccessory> CreateTemperaturePairedAccessory(CancellationToken token)
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
                                           SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData)
        {
            mockHsController.Setup(x => x.GetPropertyByRef(It.IsAny<int>(), It.IsAny<EProperty>()))
                .Returns((int devOrFeatRef, EProperty property) =>
                {
                    return deviceOrFeatureData[devOrFeatRef][property];
                });

            mockHsController.Setup(x => x.UpdateFeatureValueByRef(It.IsAny<int>(), It.IsAny<double>()))
                .Returns((int devOrFeatRef, double value) =>
                {
                    deviceOrFeatureData[devOrFeatRef][EProperty.Value] = value;
                    return true;
                });

            mockHsController.Setup(x => x.UpdatePropertyByRef(It.IsAny<int>(), It.IsAny<EProperty>(), It.IsAny<object>()))
                .Callback((int devOrFeatRef, EProperty property, object value) =>
                {
                    deviceOrFeatureData[devOrFeatRef][property] = value;
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
            mockHsController.Setup(x => x.GetRefsByInterface(PlugInData.PlugInId, true)).Returns(new List<int>());
            mockHsController.Setup(x => x.GetNameByRef(It.IsAny<int>())).Returns("Test");
            return mockHsController;
        }

        public static void VeryHtmlValid(string html)
        {
            HtmlAgilityPack.HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(html);
            Assert.AreEqual(0, htmlDocument.ParseErrors.Count());
        }
    }
}