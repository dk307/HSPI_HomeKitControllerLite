using HomeSeer.PluginSdk;
using Hspi.Utils;
using Serilog;
using System;

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

        protected override void BeforeReturnStatus()
        {
            this.Status = PluginStatus.Ok();
        }

        protected override void Initialize()
        {
            try
            {
                Log.Information("Plugin Starting");

                // Device Add Page
                HomeSeerSystem.RegisterDeviceIncPage(PlugInData.PlugInId, "AddDevice.html", "Pair HomeKit Device");

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

        public override string PostBackProc(string page, string data, string user, int userRights)
        {
            Log.Debug("PostBackProc for {page} with {data}", page, data);
            return page switch
            {
                AddDeviceHandler.PageName => AddDeviceHandler.PostBackProc(data, HomeSeerSystem, ShutdownCancellationToken),
                _ => base.PostBackProc(page, data, user, userRights),
            };
        }
    }
}