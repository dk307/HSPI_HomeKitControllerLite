using HomeKit;
using HomeKit.Model;
using HomeSeer.PluginSdk;
using Hspi.DeviceData;
using Hspi.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

#nullable enable

namespace Hspi
{
    internal static class AddDeviceHandler
    {
        public static string PostBackProc(string data,
                                          IHsController hsController,
                                          CancellationToken cancellationToken)
        {
            return PostBackProcAsync(data, hsController, cancellationToken).ResultForSync();
        }

        private sealed record Result(string? ErrorMessage = null, object? Data = null);

        private static async Task<string> PostBackProcAsync(string data,
                                                            IHsController hsController,
                                                            CancellationToken cancellationToken)
        {
            try
            {
                var requestObject = JsonConvert.DeserializeObject<JObject>(data);
                var action = requestObject["action"]?.ToString();
                return action switch
                {
                    "search" => await Discover(cancellationToken).ConfigureAwait(false),
                    "pair" => await Pair(hsController, requestObject, cancellationToken).ConfigureAwait(false),
                    _ => throw new ArgumentException("Unknown Action"),
                };
            }
            catch (Exception ex)
            {
                var result = new Result { ErrorMessage = ex.GetFullMessage() };
                return JsonConvert.SerializeObject(result);
            }
        }

        private static async ValueTask<string> Pair(IHsController hsController,
                                                    JObject requestObject,
                                                    CancellationToken cancellationToken)
        {
            var pincode = requestObject["pincode"]?.ToString();
            var discoveredDevice = requestObject["data"]?.ToObject<DiscoveredDevice>();

            if ((pincode == null) || (discoveredDevice == null))
            {
                throw new ArgumentException("Invalid data for pairing");
            }
            var pairingInfo = await InsecureConnection.StartNewPairing(discoveredDevice, pincode, cancellationToken).ConfigureAwait(false);

            using SecureConnection secureConnection = new(pairingInfo);

            await secureConnection.ConnectAndListen(discoveredDevice.Address, cancellationToken).ConfigureAwait(false);

            var accessoryInfo = secureConnection.DeviceReportedInfo;
            var accessory1Aid = accessoryInfo?.Accessories.FirstOrDefault(x => x.Aid == 1);
            if (accessory1Aid == null)
            {
                Log.Error("No Aid 1 accessor found for {name}", discoveredDevice.DisplayName);
                throw new InvalidOperationException(Invariant($"No Aid 1 accessory found for {discoveredDevice.DisplayName}"));
            }

            int refId = HomeKitDeviceFactory.CreateHsDevice(hsController,
                                                            pairingInfo,
                                                            discoveredDevice.Address,
                                                            accessory1Aid);

            Log.Information("Created {refId} for {name}", refId, discoveredDevice.DisplayName);

            var result = new Result();
            return JsonConvert.SerializeObject(result);
        }

        private static async ValueTask<string> Discover(CancellationToken cancellationToken)
        {
            var discoveredDevices = await HomeKitDiscover.DiscoverIPs(TimeSpan.FromSeconds(5), cancellationToken);
            var unPairedDevices = discoveredDevices.Where(x => (x.Status & DeviceStatus.NotPaired) == DeviceStatus.NotPaired);
            var result = new Result { Data = unPairedDevices };
            return JsonConvert.SerializeObject(result);
        }

        public const string PageName = "AddDevice.html";
    }
}