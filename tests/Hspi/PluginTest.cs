using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Nito.AsyncEx;
using Serilog;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class PlugInTest
    {
        public PlugInTest()
        {
            cancellationTokenSource.CancelAfter(60 * 1000);
        }

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
        public async Task GetDeviceList()
        {
            using var hapAccessory =
                await TestHelper.CreateTemperaturePairedAccessory(cancellationTokenSource.Token).ConfigureAwait(false);
            AsyncProducerConsumerQueue<bool> connectionStatus = new();
            var (plugIn, _) = await TestHelper.StartPluginWithHapAccessory(hapAccessory,
                                                                           connectionStatus,
                                                                           cancellationTokenSource.Token);

            var deviceList = plugIn.Object.GetDeviceList();
            Assert.IsNotNull(deviceList);
            Assert.AreEqual(1, deviceList.Count);
            Assert.AreEqual("Test", deviceList[HapAccessory.StartDeviceRefId]);
        }

        [TestMethod]
        public void GetJuiDeviceConfigPageForInvalidDevice()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            int refId = 935;
            mockHsController.Setup(x => x.GetPropertyByRef(refId, It.IsAny<EProperty>())).Returns(null);

            Assert.IsTrue(plugin.Object.InitIO());

            var errorResult = plugin.Object.GetJuiDeviceConfigPage(refId);

            var page = Page.FromJsonString(errorResult);
            Assert.IsNotNull(page);

            //assert contains error
            Assert.AreEqual("exception", page.Views[0].Id);
            Assert.IsTrue(((LabelView)page.Views[0]).ToHtml().Contains("is null"));

            plugin.Object.Dispose();
        }

        [TestMethod]
        public void InitFirstTime()
        {
            var plugin = TestHelper.CreatePlugInMock();
            var mockHsController = TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());

            Assert.IsTrue(plugin.Object.InitIO());
            plugin.Object.ShutdownIO();
            plugin.Object.Dispose();
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

        private readonly CancellationTokenSource cancellationTokenSource = new();
    }
}