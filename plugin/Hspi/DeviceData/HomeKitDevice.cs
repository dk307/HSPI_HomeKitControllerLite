using HomeKit.Model;
using HomeKit.Utils;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using Newtonsoft.Json;
using Nito.AsyncEx;
using Serilog;
using System.IO;
using System.Net;
using System.Threading;

#nullable enable

namespace Hspi.DeviceData
{
    internal sealed partial class HomeKitDevice
    {
        public HomeKitDevice(IHsController hsController,
                             int refId,
                             CancellationToken cancellationToken)
        {
            this.hsController = hsController;
            this.RefId = refId;
            this.cancellationToken = cancellationToken;
            //Utils.TaskHelper.StartAsyncWithErrorChecking(Invariant($"Device Start {refId}"),
            //                                             UpdateDeviceProperties,
            //                                             cancellationToken,
            //                                             TimeSpan.FromSeconds(15));
        }

        public int RefId { get; }




        private readonly IHsController hsController;
        private CancellationToken cancellationToken;
        private readonly AsyncMonitor featureLock = new AsyncMonitor();
    }
}