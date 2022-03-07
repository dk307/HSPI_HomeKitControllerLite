using HomeKit.Model;
using HomeSeer.Jui.Types;
using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using Hspi.DeviceData;
using System;
using System.Globalization;

#nullable enable

namespace Hspi.Pages
{
    internal static class DeviceConfigPage
    {
        public static Page BuildConfigPage(IHsController hsController, int deviceOrFeatureRef)
        {
            var device = new HsHomeKitBaseRootDevice(hsController, deviceOrFeatureRef);
            var pageFactory = PageFactory.CreateDeviceConfigPage(PlugInData.PlugInId, "HomeKit Device");

            if (device.Aid == 1)
            {
                var pairingInfo = device.PairingInfo;
                pageFactory = AddSettingView(pageFactory, pairingInfo);
            }

            var accessoryInfo = device.CachedAccessoryInfo;
            var enabledCharacteristics = device.EnabledCharacteristic;
            pageFactory = AddEnabledCharacteristicViews(pageFactory, accessoryInfo, enabledCharacteristics);

            return pageFactory.Page;
        }

        private static PageFactory AddEnabledCharacteristicViews(PageFactory pageFactory, Accessory accessoryInfo, System.Collections.Immutable.ImmutableSortedSet<ulong> enabledCharacteristics)
        {
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

            return pageFactory;
        }

        private static PageFactory AddSettingView(PageFactory pageFactory, PairingDeviceInfo pairingInfo)
        {
            var settingView = new GridView("id_setting", "Setting");

            settingView.AddView(new InputView(nameof(PairingDeviceInfo.PollingTimeSpan),
                                              "Polling interval for Non-Event Devices(seconds)",
                                              pairingInfo.PollingTimeSpan?.TotalSeconds.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                                              EInputType.Number));
            settingView.AddView(new ToggleView(nameof(PairingDeviceInfo.EnableKeepAliveForConnection),
                                               "Keep peristent connection to device",
                                               pairingInfo.EnableKeepAliveForConnection));

            pageFactory = pageFactory.WithView(settingView);
            return pageFactory;
        }

        internal static void OnDeviceConfigChange(IHsController hsController, int deviceOrFeatureRef, Page deviceConfigPage)
        {
            var device = new HsHomeKitBaseRootDevice(hsController, deviceOrFeatureRef);

            UpdatePollingInterval(device, deviceConfigPage);
            UpdateKeepAliveForConnection(device, deviceConfigPage);
        }

        private static void UpdatePollingInterval(HsHomeKitBaseRootDevice device, Page deviceConfigPage)
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

        private static void UpdateKeepAliveForConnection(HsHomeKitBaseRootDevice device, Page deviceConfigPage)
        {
            if (deviceConfigPage.ContainsViewWithId(nameof(PairingDeviceInfo.EnableKeepAliveForConnection)))
            {
                var view = deviceConfigPage.GetViewById<ToggleView>(nameof(PairingDeviceInfo.EnableKeepAliveForConnection));
                device.SetKeepAliveForConnection(view.IsEnabled);
            }
        }
    }
}