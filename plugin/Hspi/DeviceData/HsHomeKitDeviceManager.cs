using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices.Controls;
using Hspi.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Hspi.DeviceData
{
    internal sealed class HsHomeKitDeviceManager : IDisposable
    {
        public HsHomeKitDeviceManager(IHsController HS,
                                      CancellationToken cancellationToken)
        {
            this.HS = HS;
            this.combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            homeKitDevices = GetCurrentDevices().ToImmutableDictionary();
        }

        public ImmutableDictionary<int, HomeKitDevice> Devices => homeKitDevices;

        public void Dispose()
        {
            if (!disposedValue)
            {
                // this cancels the current connections
                combinedToken.Cancel();
                disposedValue = true;
            }
        }

        public async Task HandleCommand(IEnumerable<ControlEvent> colSends)
        {
            foreach (var colSend in colSends)
            {
                try
                {
                    Log.Debug("Command {command} for {RefId}",
                        colSend.ControlString ?? colSend.Label ?? colSend.ControlValue.ToString(CultureInfo.InvariantCulture),
                        colSend.TargetRef);
                    foreach (var device in homeKitDevices)
                    {
                        bool done = await device.Value.CanProcessCommand(colSend, combinedToken.Token).ConfigureAwait(false);

                        if (done)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to process {command} with {ex}", colSend.ControlValue, ex.GetFullMessage());
                }
            }
        }

        public async Task<bool> Refresh(int devOrFeatRef)
        {
            foreach (var device in homeKitDevices)
            {
                bool done = await device.Value.CanRefesh(devOrFeatRef, combinedToken.Token).ConfigureAwait(false);

                if (done)
                {
                    return true;
                }
            }
            return false;
        }

        private Dictionary<int, HomeKitDevice> GetCurrentDevices()
        {
            var interfaceRefIds = HS.GetRefsByInterface(PlugInData.PlugInId, true);

            Log.Information("Found {count} devices.", interfaceRefIds.Count);

            var homeKitDeviceIds = new List<ValueTuple<int, string>>();

            foreach (var refId in interfaceRefIds)
            {
                combinedToken.Token.ThrowIfCancellationRequested();
                try
                {
                    var pairingInfo = HsHomeKitRootDevice.GetPairingInfo(HS, refId);
                    homeKitDeviceIds.Add(new ValueTuple<int, string>(refId, pairingInfo.DeviceInformation.Id));
                }
                catch (Exception ex)
                {
                    Log.Warning("{name} has invalid plugin data and load failed with {error}. Please repair it.",
                                HsHomeKitDevice.GetNameForLog(HS, refId),
                                ex.GetFullMessage());
                }
            }

            var devices = new Dictionary<int, HomeKitDevice>();

            // group devices by id
            foreach (var group in homeKitDeviceIds.ToLookup(x => x.Item2))
            {
                var refIdsForDevice = group.Select(x => x.Item1);
                var device = new HomeKitDevice(HS, refIdsForDevice, combinedToken.Token);

                foreach (var refIds in refIdsForDevice)
                {
                    devices.Add(refIds, device);
                }
            }

            return devices;
        }

        private readonly CancellationTokenSource combinedToken;
        private readonly ImmutableDictionary<int, HomeKitDevice> homeKitDevices;
        private readonly IHsController HS;
        private bool disposedValue;
    }
}