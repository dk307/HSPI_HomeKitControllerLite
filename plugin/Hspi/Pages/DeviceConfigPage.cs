using HomeKit.Model;
using HomeSeer.Jui.Types;
using HomeSeer.Jui.Views;
using Hspi.DeviceData;
using System.Globalization;

#nullable enable

namespace Hspi.Pages
{
    internal static class DeviceConfigPage
    {
        public static Page BuildConfigPage(int deviceOrFeatureRef, HomeKitDevice homeKitDevice)
        {
            var pageFactory = PageFactory.CreateDeviceConfigPage(PlugInData.PlugInId, "HomeKit Device");

            var accessoryInfo = homeKitDevice.GetAccessoryInfo(deviceOrFeatureRef);
            var pairingInfo = homeKitDevice.GetPairingInfo(deviceOrFeatureRef);
            var enabledCharacteristics = homeKitDevice.GetEnabledCharacteristic(deviceOrFeatureRef);

            pageFactory = AddSettingView(pageFactory, pairingInfo);

            foreach (var service in accessoryInfo.Services)
            {
                var selectCharacteristicsView = new GridView("id_service_" + service.Key.ToString(CultureInfo.InvariantCulture),
                                                             "Enable Characteristics - " + service.Value.Type.DisplayName);

                foreach (var characteristic in service.Value.Characteristics)
                {
                    ToggleView view = new("id_service_" + characteristic.Key.ToString(CultureInfo.InvariantCulture),
                                          characteristic.Value.Type.DisplayName ?? "Unknown - " + characteristic.Value.Type.Id.ToString(),
                                          enabledCharacteristics.Contains(characteristic.Key));
                    selectCharacteristicsView.AddView(view);
                }

                pageFactory = pageFactory.WithView(selectCharacteristicsView);
            }

            return pageFactory.Page;
        }

        private static PageFactory AddSettingView(PageFactory pageFactory, PairingDeviceInfo? pairingInfo)
        {
            var settingView = new GridView("id_setting", "Setting");

            settingView.AddView(new InputView(nameof(pairingInfo.PollingTimeSpan),
                                              "Polling interval for Non-Event Devices(seconds)",
                                              pairingInfo?.PollingTimeSpan?.TotalSeconds.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                                              EInputType.Number));
            settingView.AddView(new ToggleView(nameof(pairingInfo.EnableKeepAliveForConnection),
                                               "Keep peristent connection to device",
                                               pairingInfo.EnableKeepAliveForConnection));

            pageFactory = pageFactory.WithView(settingView);
            return pageFactory;
        }
    }
}