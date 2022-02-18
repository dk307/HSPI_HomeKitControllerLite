using HomeKit.Model;
using Hspi.Utils;
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
        public static async Task<DiscoveredDevice?> DiscoverDeviceById(string id, TimeSpan scanTime,
                                                                     CancellationToken cancellationToken)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token,
                                                                           cancellationToken);

            DiscoveredDevice? device = null;
            Action<IZeroconfHost> callback = (IZeroconfHost host) =>
            {
                if (IsValidHomeKitDevice(host) && device == null)
                {
                    var deviceFound = DiscoveredDevice.FromZeroConfigHost(host);
                    if (deviceFound.Id == id)
                    {
                        device = deviceFound;
                        cancellationTokenSource.Cancel();
                    }
                }
            };

            try
            {
                await ZeroconfResolver.ResolveAsync(DiscoveredDevice.HapProtocol,
                                                                  scanTime: scanTime,
                                                                  callback: callback,
                                                                  cancellationToken: combined.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex.IsCancelException())
            {
                //ignore
            }
            return device;
        }

        public static async Task<IList<DiscoveredDevice>> DiscoverIPs(TimeSpan scanTime,
                                                                      CancellationToken cancellationToken)
        {
            var devices = await ZeroconfResolver.ResolveAsync(DiscoveredDevice.HapProtocol,
                                                              scanTime: scanTime,
                                                              cancellationToken: cancellationToken).ConfigureAwait(false);

            var homekitDevices = devices.SkipWhile(x => !IsValidHomeKitDevice(x))
                                        .Select(x => DiscoveredDevice.FromZeroConfigHost(x));

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

            var hapKey = device.Services.Keys.FirstOrDefault(x => x.EndsWith(DiscoveredDevice.HapProtocol));

            if (hapKey != null)
            {
                //  create a flat list of all properties of  service
                var service = device.Services[hapKey];
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