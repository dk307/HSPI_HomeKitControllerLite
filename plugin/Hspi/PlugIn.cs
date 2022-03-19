using HomeSeer.Jui.Views;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using Hspi.DeviceData;
using Hspi.Pages;
using Hspi.Utils;
using Nito.AsyncEx;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

#nullable enable

namespace Hspi
{
    internal class PlugIn : HspiBase
    {
        public PlugIn()
            : base(PlugInData.PlugInId, PlugInData.PlugInName)
        {
        }

        public override bool SupportsConfigDevice => true;

        public override string GetJuiDeviceConfigPage(int devOrFeatRef)
        {
            try
            {
                Log.Debug("Asking for page for {deviceOrFeatureRef}", devOrFeatRef);

                var page = DeviceConfigPage.BuildConfigPage(HomeSeerSystem, devOrFeatRef);

                var devicePage = page?.ToJsonString() ?? throw new InvalidOperationException("Page is unexpectedly null");
                Log.Debug("Returning page for {deviceOrFeatureRef}", devOrFeatRef);
                return devicePage;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to create page for {devOrFeatRef} with error:{error}", devOrFeatRef, ex.GetFullMessage());
                var page = PageFactory.CreateDeviceConfigPage(PlugInData.PlugInId, "Z-Wave Information");
                page = page.WithView(new LabelView("exception", string.Empty, ex.GetFullMessage())
                {
                    LabelType = HomeSeer.Jui.Types.ELabelType.Preformatted
                });
                return page.Page.ToJsonString();
            }
        }

        public override string PostBackProc(string page, string data, string user, int userRights)
        {
            Log.Debug("PostBackProc for {page} with {data}", page, data);
            var (result, restart) = page switch
            {
                AddDeviceHandler.PageName => AddDeviceHandler.PostBackProc(data,
                                                                           HomeSeerSystem,
                                                                           ShutdownCancellationToken),
                UnpairDeviceHandler.PageName => UnpairDeviceHandler.PostBackProc(data,
                                                                                 HomeSeerSystem,
                                                                                 GetHomeKitDeviceManager(),
                                                                                 ShutdownCancellationToken),
                _ => (base.PostBackProc(page, data, user, userRights), false),
            };

            if (restart)
            {
                RestartProcessing();
            }

            return result;
        }

        public override void SetIOMulti(List<ControlEvent> colSend)
        {
            try
            {
                SetIOMultiAsync().ResultForSync();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to process commands with {ex}", ex.GetFullMessage());
            }

            async Task SetIOMultiAsync()
            {
                var deviceManagerCopy = await GetHomeKitDeviceManager().ConfigureAwait(false);
                await deviceManagerCopy.HandleCommand(colSend).ConfigureAwait(false);
            }
        }

        protected override void BeforeReturnStatus()
        {
            this.Status = PluginStatus.Ok();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                deviceManager?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void Initialize()
        {
            try
            {
                UpdateDebugLevel();
                Log.Information("Plugin Starting");
                Settings.Add(SettingsPages.CreateDefault());
                LoadSettingsFromIni();
                settingsPages = new SettingsPages(Settings);
                UpdateDebugLevel();

                // Device Add Page
                HomeSeerSystem.RegisterDeviceIncPage(PlugInData.PlugInId, "AddDevice.html", "Pair HomeKit Device");

                // Other Pages
                HomeSeerSystem.RegisterFeaturePage(PlugInData.PlugInId, "UnpairDevice.html", "Unpair HomeKit Device");

                RestartProcessing();

                Log.Information("Plugin Started");
            }
            catch (Exception ex)
            {
                Log.Error("Failed to initialize PlugIn with {error}", ex.GetFullMessage());
                throw;
            }
        }

        protected override bool OnDeviceConfigChange(Page deviceConfigPage, int devOrFeatRef)
        {
            DeviceConfigPage.OnDeviceConfigChange(HomeSeerSystem, devOrFeatRef, deviceConfigPage);
            RestartProcessing();
            return true;
        }

        protected override bool OnSettingChange(string pageId, AbstractView currentView, AbstractView changedView)
        {
            Log.Information("Page:{pageId} has changed value of id:{id} to {value}", pageId, changedView.Id, changedView.GetStringValue());

            CheckNotNull(settingsPages);

            if (settingsPages.OnSettingChange(changedView))
            {
                UpdateDebugLevel();
                return true;
            }

            return base.OnSettingChange(pageId, currentView, changedView);
        }

        protected override void OnShutdown()
        {
            Log.Information("Shutting down");
            base.OnShutdown();
        }

        private static void CheckNotNull([NotNull] object? obj)
        {
            if (obj is null)
            {
                throw new InvalidOperationException("Plugin Not Initialized");
            }
        }

        private async Task<HsHomeKitDeviceManager> GetHomeKitDeviceManager()
        {
            using var _ = await dataLock.LockAsync(ShutdownCancellationToken);
            return deviceManager ?? throw new InvalidOperationException("No Devices Found. Initialize in progress");
        }

        private async Task MainTask()
        {
            using var sync = await dataLock.LockAsync(ShutdownCancellationToken).ConfigureAwait(false);

            deviceManager?.Dispose();
            deviceManager = new HsHomeKitDeviceManager(HomeSeerSystem,
                                                       ShutdownCancellationToken);
        }

        private void RestartProcessing()
        {
            Utils.TaskHelper.StartAsyncWithErrorChecking("Main Task",
                                                          MainTask,
                                                          ShutdownCancellationToken,
                                                          TimeSpan.FromSeconds(10));
        }

        private void UpdateDebugLevel()
        {
            bool debugLevel = true;
            bool logToFile = false;
            this.LogDebug = debugLevel;
            Logger.ConfigureLogging(LogDebug, logToFile, HomeSeerSystem);
        }

        public override EPollResponse UpdateStatusNow(int devOrFeatRef)
        {
            Log.Information("Polling for {devOrFeatRef}", devOrFeatRef);

            try
            {
                bool processed = UpdateStatusNowAsync().ResultForSync();
                return processed ? EPollResponse.Ok : EPollResponse.NotFound;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to poll {devOrFeatRef} with {ex}", devOrFeatRef, ex.GetFullMessage());
                return EPollResponse.UnknownError;
            }

            async Task<bool> UpdateStatusNowAsync()
            {
                var deviceManagerCopy = await GetHomeKitDeviceManager().ConfigureAwait(false);
                return await deviceManagerCopy.Refresh(devOrFeatRef).ConfigureAwait(false);
            }
        }

        // used by scrbian
        public IDictionary<int, string> GetDeviceList()
        {
            var result = new Dictionary<int, string>();
            foreach (var refId in HomeSeerSystem.GetRefsByInterface(PlugInData.PlugInId, true))
            {
                var rootDevice = new HsHomeKitBaseRootDevice(HomeSeerSystem, refId);
                if (rootDevice.Aid == 1)
                {
                    result.Add(refId, HsHomeKitDevice.GetNameForLog(HomeSeerSystem, refId));
                }
            }
            return result;
        }

        private readonly AsyncLock dataLock = new();
        private volatile HsHomeKitDeviceManager? deviceManager;
        private SettingsPages? settingsPages;
    }
}