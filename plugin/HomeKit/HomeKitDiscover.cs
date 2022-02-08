using HomeKit.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zeroconf;

#nullable enable

namespace HomeKit
{
    internal static class HomeKitDiscover
    {
        public static async Task<PairingDeviceInfo> Pair(Device deviceInformation,
                                                         string pin,
                                                         CancellationToken cancellationToken)
        {
            using var connection = new InsecureConnection(deviceInformation);
            await connection.ConnectAndListen(cancellationToken).ConfigureAwait(false);

            return await connection.StartNewPairing(pin, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<IList<DiscoveredDevice>> DiscoverIPs(TimeSpan scanTime,
                                                               CancellationToken cancellationToken)
        {
            var devices = await ZeroconfResolver.ResolveAsync(DiscoveredDevice.HapProtocol,
                                                              scanTime: scanTime,
                                                              cancellationToken: cancellationToken).ConfigureAwait(false);

            var homekitDevices = devices.SkipWhile(x => !IsValidHomeKitDevice(x))
                                        .Select(x => new DiscoveredDevice(x));

            // devices can show up multiple times
            var consolidatedHomekitDevices = new List<DiscoveredDevice>();

            foreach (var groupedDevices in homekitDevices.GroupBy(x => x.Id))
            {
                consolidatedHomekitDevices.Add(groupedDevices.First());
            }

            return consolidatedHomekitDevices;
        }

        private static bool IsValidHomeKitDevice(IZeroconfHost device)
        {
            if (device.IPAddress.Length == 0)
            {
                return false;
            }

            if (device.Services.TryGetValue(DiscoveredDevice.HapProtocol, out var service))
            {
                //  create a flat list of all properties of  service
                var properties = service.Properties.SelectMany(x => x.Keys).ToList();
                return properties.Contains("c#") &&  // Current configuration number
                       properties.Contains("md") &&  // Model name of the accessory
                       properties.Contains("s#") &&  // Current state number
                       properties.Contains("id");    // Device ID
            }

            return false;
        }
    }
}