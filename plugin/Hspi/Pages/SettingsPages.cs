using System;
using HomeSeer.Jui.Views;
using Hspi.Utils;
using Serilog.Core;
using Serilog.Events;
using System.Linq;
using System.Buffers;
using System.Globalization;

#nullable enable

namespace Hspi
{
    internal sealed class SettingsPages
    {
        public SettingsPages(SettingsCollection collection)
        {
            if (!Enum.TryParse<LogEventLevel>(collection[SettingPageId].GetViewById<SelectListView>(LoggingLevelId).GetSelectedOptionKey(), out LogEventLevel logEventLevel))
            {
                LogLevel = LogEventLevel.Information;
            }
            else
            {
                LogLevel = logEventLevel;
            }
            LogtoFileEnabled = collection[SettingPageId].GetViewById<ToggleView>(LogToFileId).IsEnabled;
        }

        public LogEventLevel LogLevel { get; private set; }

        public bool DebugLoggingEnabled => LogLevel <= LogEventLevel.Debug;

        public bool LogtoFileEnabled { get; private set; }

        public bool LogValueChangeEnabled { get; private set; }

        public static Page CreateDefault(LogEventLevel logEventLevel = LogEventLevel.Information,
                                         bool logToFileDefault = false,
                                         bool logValueChangeDefault = false)
        {
            var settings = PageFactory.CreateSettingsPage(SettingPageId, "Settings");

            var logOptions = EnumHelper.GetValues<LogEventLevel>().Select(x => x.ToString()).ToList();
            settings = settings.WithDropDownSelectList(LoggingLevelId, "Logging Level", logOptions, logOptions,(int)logEventLevel);
            settings = settings.WithToggle(LogToFileId, "Log to file", logToFileDefault);
            settings = settings.WithLabel("icon_id", "<a class='float-right' href='https://icons8.com'>Icons from Icons8</a>");
            return settings.Page;
        }

        public bool OnSettingChange(AbstractView changedView)
        {
            if (changedView.Id == LoggingLevelId)
            {
                var value = ((SelectListView)changedView).GetSelectedOptionKey();
                if (Enum.TryParse<LogEventLevel>(value, out LogEventLevel logEventLevel)) {
                    LogLevel = logEventLevel;
                    return true;
                }
                return false;
            }

            if (changedView.Id == LogToFileId)
            {
                LogtoFileEnabled = ((ToggleView)changedView).IsEnabled;
                return true;
            }

            return false;
        }

        internal const string LoggingLevelId = "LogLevel";
        internal const string LogToFileId = "LogToFile";
        internal const string SettingPageId = "setting_page_id";
    }
}