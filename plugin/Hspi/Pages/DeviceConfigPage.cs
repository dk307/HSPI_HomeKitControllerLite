using HomeKit.Model;
using HomeSeer.Jui.Types;
using HomeSeer.Jui.Views;
using Hspi.DeviceData;
using System;
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
                    ToggleView view = new("id_char_" + characteristic.Key.ToString(CultureInfo.InvariantCulture),
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

            settingView.AddView(new InputView(nameof(PairingDeviceInfo.PollingTimeSpan),
                                              "Polling interval for Non-Event Devices(seconds)",
                                              pairingInfo?.PollingTimeSpan?.TotalSeconds.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                                              EInputType.Number));
            settingView.AddView(new ToggleView(nameof(PairingDeviceInfo.EnableKeepAliveForConnection),
                                               "Keep peristent connection to device",
                                               pairingInfo.EnableKeepAliveForConnection));

            pageFactory = pageFactory.WithView(settingView);
            return pageFactory;
        }

        internal static void OnDeviceConfigChange(int deviceRef, HomeKitDevice device, Page deviceConfigPage)
        {
            UpdatePollingInterval(device, deviceConfigPage);
            UpdateKeepAliveForConnection(device, deviceConfigPage);
        }

        private static void UpdatePollingInterval(HomeKitDevice device, Page deviceConfigPage)
        {
            if (deviceConfigPage.ContainsViewWithId(nameof(PairingDeviceInfo.PollingTimeSpan)))
            {
                TimeSpan? pollingTimeSpan;
                var pollingView = deviceConfigPage.GetViewById<InputView>(nameof(PairingDeviceInfo.PollingTimeSpan));

                if (!string.IsNullOrWhiteSpace(pollingView.Value))
                {
                    if (double.TryParse(pollingView.Value, out var polling))
                    {
                        pollingTimeSpan = TimeSpan.FromSeconds(polling);
                    }
                    else
                    {
                        throw new ArgumentException("Polling interval not valid");
                    }
                }
                else
                {
                    pollingTimeSpan = null;
                }

                device.SetPollingInterval(pollingTimeSpan);
            }
        }

        private static void UpdateKeepAliveForConnection(HomeKitDevice device, Page deviceConfigPage)
        {
            if (deviceConfigPage.ContainsViewWithId(nameof(PairingDeviceInfo.EnableKeepAliveForConnection)))
            {
                var view = deviceConfigPage.GetViewById<ToggleView>(nameof(PairingDeviceInfo.EnableKeepAliveForConnection));
                device.SetKeepAliveForConnection(view.IsEnabled);
            }
        }
    }
}