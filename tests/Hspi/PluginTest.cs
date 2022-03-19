using HomeSeer.Jui.Views;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using System.Collections.Generic;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class PlugInTest
    {
        [TestMethod]
        public void CheckDebugLevelSettingChange()
        {
            var (plugInMock, _) = TestHelper.CreateMockPluginAndHsController(new Dictionary<string, string>());

            PlugIn plugIn = plugInMock.Object;

            var settingsCollection = new SettingsCollection
            {
                // invert all default values
                SettingsPages.CreateDefault(enableDebugLoggingDefault : true,
                                            logToFileDefault : true)
            };

            Assert.IsTrue(plugIn.SaveJuiSettingsPages(settingsCollection.ToJsonString()));
            Assert.IsTrue(Log.Logger.IsEnabled(Serilog.Events.LogEventLevel.Debug));
            plugInMock.Verify();
        }

        [TestMethod]
        public void CheckSettingsWithIniFilledDuringInitialize()
        {
            var settingsFromIni = new Dictionary<string, string>()
            {
                { SettingsPages.LoggingDebugId, true.ToString()},
                { SettingsPages.LogToFileId, true.ToString()},
            };

            var (plugInMock, _) = TestHelper.CreateMockPluginAndHsController(settingsFromIni);

            PlugIn plugIn = plugInMock.Object;

            Assert.IsTrue(plugIn.HasSettings);

            var settingPages = SettingsCollection.FromJsonString(plugIn.GetJuiSettingsPages());
            Assert.IsNotNull(settingPages);

            var settings = settingPages[SettingsPages.SettingPageId].ToValueMap();

            Assert.AreEqual(settings[SettingsPages.LoggingDebugId], true.ToString());
            Assert.AreEqual(settings[SettingsPages.LogToFileId], true.ToString());
        }

        [TestMethod]
        public void InitFirstTime()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            Assert.IsTrue(plugin.Object.InitIO());
            plugin.Object.ShutdownIO();
            mockHsController.Verify(x => x.RegisterDeviceIncPage(PlugInData.PlugInId, "AddDevice.html", "Pair HomeKit Device"));
            mockHsController.Verify(x => x.RegisterFeaturePage(PlugInData.PlugInId, "UnpairDevice.html", "Unpair HomeKit Device"));
        }

        [TestMethod]
        public void PostBackProcforNonHandled()
        {
            var plugin = new PlugIn();
            Assert.AreEqual(plugin.PostBackProc("Random", "data", "user", 0), string.Empty);
        }

        [TestMethod]
        public void SupportsDeviceConfigPage()
        {
            var plugin = new PlugIn();
            Assert.IsTrue(plugin.SupportsConfigDevice);
        }

        [TestMethod]
        public void VerifyNameAndId()
        {
            var plugin = new PlugIn();
            Assert.AreEqual(plugin.Id, PlugInData.PlugInId);
            Assert.AreEqual(plugin.Name, PlugInData.PlugInName);
        }
    }
}