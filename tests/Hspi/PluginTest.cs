using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk.Devices;
using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Nito.AsyncEx;
using Serilog;
using Serilog.Events;
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

        [DataTestMethod]
        [DataRow(LogEventLevel.Information, false)]
        [DataRow(LogEventLevel.Warning, false)]
        [DataRow(LogEventLevel.Fatal, false)]
        [DataRow(LogEventLevel.Information, true)]
        [DataRow(LogEventLevel.Verbose, false)]
        [DataRow(LogEventLevel.Debug, true)]
        public void CheckLogLevelSettingChange(LogEventLevel logEventLevel, bool logToFile)
        {
            var settingsFromIni = new Dictionary<string, string>
            {
                { "LogLevelId", (logEventLevel == LogEventLevel.Verbose ? LogEventLevel.Fatal : LogEventLevel.Verbose).ToString() },
                { "LogToFileId", logToFile.ToString() }
            };

            var (plugInMock, _) = TestHelper.CreateMockPluginAndHsController(settingsFromIni);

            PlugIn plugIn = plugInMock.Object;

            var settingsCollection = new SettingsCollection
            { 
                SettingsPages.CreateDefault(logEventLevel, logToFile)
            };

            Assert.IsTrue(plugIn.SaveJuiSettingsPages(settingsCollection.ToJsonString()));

            Assert.AreEqual(Log.Logger.IsEnabled(LogEventLevel.Fatal), logEventLevel <= LogEventLevel.Fatal);
            Assert.AreEqual(Log.Logger.IsEnabled(LogEventLevel.Error), logEventLevel <= LogEventLevel.Error);
            Assert.AreEqual(Log.Logger.IsEnabled(LogEventLevel.Warning), logEventLevel <= LogEventLevel.Warning);
            Assert.AreEqual(Log.Logger.IsEnabled(LogEventLevel.Information), logEventLevel <= LogEventLevel.Information);
            Assert.AreEqual(Log.Logger.IsEnabled(LogEventLevel.Debug), logEventLevel <= LogEventLevel.Debug);
            Assert.AreEqual(Log.Logger.IsEnabled(LogEventLevel.Verbose), logEventLevel <= LogEventLevel.Verbose);
            plugInMock.Verify();
        }

        [TestMethod]
        public void CheckSettingsWithIniFilledDuringInitialize()
        {
            var settingsFromIni = new Dictionary<string, string>()
            {
                { SettingsPages.LoggingLevelId, ((int)LogEventLevel.Information).ToString()},
                { SettingsPages.LogToFileId, true.ToString()},
            };

            var (plugInMock, _) = TestHelper.CreateMockPluginAndHsController(settingsFromIni);

            PlugIn plugIn = plugInMock.Object;

            Assert.IsTrue(plugIn.HasSettings);

            var settingPages = SettingsCollection.FromJsonString(plugIn.GetJuiSettingsPages());
            Assert.IsNotNull(settingPages);

            var settings = settingPages[SettingsPages.SettingPageId].ToValueMap();

            Assert.AreEqual(settings[SettingsPages.LoggingLevelId], ((int)LogEventLevel.Information).ToString());
            Assert.AreEqual(settings[SettingsPages.LogToFileId], true.ToString());
        }

        [TestMethod]
        public async Task GetHomekitDeviceList()
        {
            using var hapAccessory =
                await TestHelper.CreateTemperaturePairedAccessory(cancellationTokenSource.Token).ConfigureAwait(false);
            AsyncProducerConsumerQueue<bool> connectionStatus = new();
            var (plugIn, _) = await TestHelper.StartPluginWithHapAccessory(hapAccessory,
                                                                           connectionStatus,
                                                                           cancellationTokenSource.Token);

            var deviceList = plugIn.Object.GetHomekitDeviceList();
            Assert.IsNotNull(deviceList);
            Assert.AreEqual(1, deviceList.Count);
            Assert.AreEqual(HapAccessory.StartDeviceRefId, deviceList[0]);
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
            mockHsController.Verify(x => x.RegisterDeviceIncPage(PlugInData.PlugInId, "AddOrRepairDevice.html", "Add or Repair Device"));
            mockHsController.Verify(x => x.RegisterFeaturePage(PlugInData.PlugInId, "UnpairDevice.html", "Unpair Device"));
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
            Assert.AreEqual(PlugInData.PlugInId, plugin.Id);
            Assert.AreEqual(PlugInData.PlugInName, plugin.Name);
        }

        private readonly CancellationTokenSource cancellationTokenSource = new();
    }
}