using System.Linq;
using HomeSeer.Jui.Types;
using HomeSeer.Jui.Views;
using Hspi;
using Hspi.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog.Events;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class SettingsPagesTest
    {
        [TestMethod]
        public void CreateDefault()
        {
            var page = SettingsPages.CreateDefault();

            Assert.IsNotNull(page);

            foreach (var view in page.Views)
            {
                TestHelper.VerifyHtmlValid(view.ToHtml());
            }

            TestHelper.VerifyHtmlValid(page.ToHtml());

            Assert.IsTrue(page.ContainsViewWithId(SettingsPages.LogToFileId));
            Assert.IsTrue(page.ContainsViewWithId(SettingsPages.LoggingLevelId));
        }

        [DataTestMethod]
        [DataRow(LogEventLevel.Information, false)]
        [DataRow(LogEventLevel.Warning, false)]
        [DataRow(LogEventLevel.Fatal, false)]
        [DataRow(LogEventLevel.Information, true)]
        [DataRow(LogEventLevel.Verbose, false)]
        [DataRow(LogEventLevel.Debug, true)]
        public void DefaultValues(LogEventLevel logEventLevel,
                                  bool logToFileEnable)
        {
            var settingsCollection = new SettingsCollection
            {
                SettingsPages.CreateDefault(logEventLevel, logToFileEnable)
            };

            var settingPages = new SettingsPages(settingsCollection);

            Assert.AreEqual(settingPages.LogLevel, logEventLevel);
            Assert.AreEqual(settingPages.DebugLoggingEnabled, logEventLevel <= LogEventLevel.Debug);
            Assert.AreEqual(settingPages.LogtoFileEnabled, logToFileEnable);
        }

        [TestMethod]
        public void OnSettingChangeWithNoChange()
        {
            var settingsCollection = new SettingsCollection
            {
                SettingsPages.CreateDefault()
            };
            var settingPages = new SettingsPages(settingsCollection);

            Assert.IsFalse(settingPages.OnSettingChange(new ToggleView("id", "name")));
        }

        [DataTestMethod]
        [DataRow(LogEventLevel.Fatal)]
        [DataRow(LogEventLevel.Warning)]
        [DataRow(LogEventLevel.Error)]
        [DataRow(LogEventLevel.Information)]
        [DataRow(LogEventLevel.Debug)]
        [DataRow(LogEventLevel.Verbose)]
        public void OnSettingChangeWithLogLevelChange(LogEventLevel logEventLevel)
        {
            var settingsCollection = new SettingsCollection
            {
                SettingsPages.CreateDefault(logEventLevel: (logEventLevel == LogEventLevel.Verbose ? LogEventLevel.Fatal : LogEventLevel.Verbose))
            };
            var settingPages = new SettingsPages(settingsCollection);

            var logOptions = EnumHelper.GetValues<LogEventLevel>().Select(x => x.ToString()).ToList();
            SelectListView changedView = new(SettingsPages.LoggingLevelId, "name", logOptions, logOptions, ESelectListType.DropDown,
                                             (int)logEventLevel);
            Assert.IsTrue(settingPages.OnSettingChange(changedView));
            Assert.AreEqual(settingPages.LogLevel, logEventLevel);
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void OnSettingChangeWithLogtoFileChange(bool initialValue)
        {
            var settingsCollection = new SettingsCollection
            {
                SettingsPages.CreateDefault(logToFileDefault: initialValue)
            };
            var settingPages = new SettingsPages(settingsCollection);

            ToggleView changedView = new(SettingsPages.LogToFileId, "name", !initialValue);
            Assert.IsTrue(settingPages.OnSettingChange(changedView));
            Assert.AreEqual(settingPages.LogtoFileEnabled, !initialValue);
        }
    }
}