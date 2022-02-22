using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Identification;
using Hspi.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

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
            devices = GetCurrentDevices().ToImmutableDictionary();
        }

        public ImmutableDictionary<int, HomeKitDevice> Devices => devices;

        public void Dispose()
        {
            if (!disposedValue)
            {
                // this cancels the current connections
                combinedToken.Cancel();
                disposedValue = true;
            }
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
                    var relationship = (ERelationship)HS.GetPropertyByRef(refId, EProperty.Relationship);
                    if (relationship == ERelationship.Device)
                    {
                        var pairingInfo = HsHomeKitRootDevice.GetPairingInfo(HS, refId);
                        homeKitDeviceIds.Add(new ValueTuple<int, string>(refId, pairingInfo.DeviceInformation.Id));
                    }
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
        private readonly IHsController HS;
        private readonly ImmutableDictionary<int, HomeKitDevice> devices;
        private bool disposedValue;
    };
}