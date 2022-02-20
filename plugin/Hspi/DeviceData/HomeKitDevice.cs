using HomeSeer.PluginSdk;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

#nullable enable

namespace Hspi.DeviceData
{

    internal sealed class HomeKitDevice
    {
        public HomeKitDevice(IHsController hsController,
                             IEnumerable<int> refIds,
                             CancellationToken cancellationToken)
        {
            this.hsController = hsController;
            RefIds = refIds;
            this.cancellationToken = cancellationToken;
            string name = String.Join(",", refIds.Select(x => x.ToString(CultureInfo.InvariantCulture)));
            Utils.TaskHelper.StartAsyncWithErrorChecking(Invariant($"Device Start {name}"),
                                                         UpdateDeviceProperties,
                                                         cancellationToken,
                                                         TimeSpan.FromSeconds(15));
        }

        private async Task UpdateDeviceProperties()
        {
            using var _ = await featureLock.EnterAsync(cancellationToken).ConfigureAwait(false);
        }

        public IEnumerable<int> RefIds { get; }

        private readonly IHsController hsController;
        private readonly CancellationToken cancellationToken;
        private readonly AsyncMonitor featureLock = new();
    }
}