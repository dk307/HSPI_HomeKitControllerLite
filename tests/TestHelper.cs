using HomeKit.Model;
using HomeSeer.PluginSdk;
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

namespace HSPI_HomeKitControllerTest
{
    internal static class TestHelper
    {
        public static Mock<PlugIn> CreatePlugInMock()
        {
            return new Mock<PlugIn>(MockBehavior.Loose)
            {
                CallBase = true,
            };
        }

        public static HapAccessory CreateTemperaturePairedAccessory(
                    string script = "temperature_sensor_paried.py")
        {
            int port = 50001;
            string address = "0.0.0.0";
            string fileName = Path.Combine("scripts", "temperaturesensor_accessory.txt");
            string fileName2 = Path.Combine("scripts", "temperaturesensor_accessory2.txt");

            File.Copy(fileName, fileName2, true);

            string args = $"{port} {address} {fileName2}";
            var hapAccessory = new HapAccessory(script, args);
            return hapAccessory;
        }

        public static PairingDeviceInfo GetTemperatureSensorParingInfo()
        {
            string controllerFile = Path.Combine("scripts", "temperaturesensor_controller.txt");
            var controllerFileData = File.ReadAllText(controllerFile, Encoding.UTF8);

            var pairingInfo = JsonConvert.DeserializeObject<PairingDeviceInfo>(controllerFileData);
            return pairingInfo;
        }
        public static Mock<IHsController> SetupHsControllerAndSettings(Mock<PlugIn> mockPlugin,

                                                         Dictionary<string, string> settingsFromIni)
        {
            var mockHsController = new Mock<IHsController>(MockBehavior.Strict);

            // set mock homeseer via reflection
            Type plugInType = typeof(AbstractPlugin);
            var method = plugInType.GetMethod("set_HomeSeerSystem", BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance);
            method.Invoke(mockPlugin.Object, new object[] { mockHsController.Object });

            mockHsController.Setup(x => x.GetIniSection("Settings", PlugInData.PlugInId + ".ini")).Returns(settingsFromIni);
            mockHsController.Setup(x => x.SaveINISetting("Settings", It.IsAny<string>(), It.IsAny<string>(), PlugInData.PlugInId + ".ini"));
            mockHsController.Setup(x => x.WriteLog(It.IsAny<ELogType>(), It.IsAny<string>(), PlugInData.PlugInName, It.IsAny<string>()));
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