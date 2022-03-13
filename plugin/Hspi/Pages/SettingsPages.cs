using HomeSeer.Jui.Views;

#nullable enable

namespace Hspi
{
    internal sealed class SettingsPages
    {
        public SettingsPages(SettingsCollection collection)
        {
            DebugLoggingEnabled = collection[SettingPageId].GetViewById<ToggleView>(LoggingDebugId).IsEnabled;
            LogtoFileEnabled = collection[SettingPageId].GetViewById<ToggleView>(LogToFileId).IsEnabled;
        }

        public bool DebugLoggingEnabled { get; private set; }

        public bool LogtoFileEnabled { get; private set; }

        public static Page CreateDefault(bool enableDebugLoggingDefault = false,
                                         bool logToFileDefault = false)
        {
            var settings = PageFactory.CreateSettingsPage(SettingPageId, "Settings");
            settings = settings.WithToggle(LoggingDebugId, "Enable debug logging", enableDebugLoggingDefault);
            settings = settings.WithToggle(LogToFileId, "Log to file", logToFileDefault);
            settings = settings.WithLabel("icon_id", "<a href='https://icons8.com'>Icons from Icons8</a>");
            return settings.Page;
        }

        public bool OnSettingChange(AbstractView changedView)
        {
            if (changedView.Id == LoggingDebugId)
            {
                DebugLoggingEnabled = ((ToggleView)changedView).IsEnabled;
                return true;
            }

            if (changedView.Id == LogToFileId)
            {
                LogtoFileEnabled = ((ToggleView)changedView).IsEnabled;
                return true;
            }

            return false;
        }

        internal const string LoggingDebugId = "DebugLogging";
        internal const string LogToFileId = "LogToFile";
        internal const string SettingPageId = "setting_page_id";
    }
}