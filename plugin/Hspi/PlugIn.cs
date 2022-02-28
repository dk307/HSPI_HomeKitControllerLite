using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices.Controls;
using Hspi.DeviceData;
using Hspi.Utils;
using Nito.AsyncEx;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

        public override bool SupportsConfigDeviceAll => true;

        public override string PostBackProc(string page, string data, string user, int userRights)
        {
            Log.Debug("PostBackProc for {page} with {data}", page, data);
            var (result, restart) = page switch
            {
                AddDeviceHandler.PageName => AddDeviceHandler.PostBackProc(data, HomeSeerSystem, ShutdownCancellationToken),
                _ => (base.PostBackProc(page, data, user, userRights), false),
            };

            if (restart)
            {
                RestartProcessing();
            }

            return result;
        }

        public override void SetIOMulti(List<ControlEvent> colSends)
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
                deviceManagerCopy?.HandleCommand(colSends);
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

                // Device Add Page
                HomeSeerSystem.RegisterDeviceIncPage(PlugInData.PlugInId, "AddDevice.html", "Pair HomeKit Device");

                RestartProcessing();

                Log.Information("Plugin Started");
            }
            catch (Exception ex)
            {
                Log.Error("Failed to initialize PlugIn with {error}", ex.GetFullMessage());
                throw;
            }
        }

        protected override void OnShutdown()
        {
            Log.Information("Shutting down");
            base.OnShutdown();
        }

        private async Task<ImmutableDictionary<int, HomeKitDevice>> GetDevices()
        {
            using var _ = await dataLock.LockAsync(ShutdownCancellationToken);
            return deviceManager?.Devices ??
                    ImmutableDictionary<int, HomeKitDevice>.Empty;
        }

        private async ValueTask<HsHomeKitDeviceManager?> GetHomeKitDeviceManager()
        {
            using var _ = await dataLock.LockAsync(ShutdownCancellationToken);
            return deviceManager;
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
        private readonly AsyncLock dataLock = new();
        private volatile HsHomeKitDeviceManager? deviceManager;
    }
}