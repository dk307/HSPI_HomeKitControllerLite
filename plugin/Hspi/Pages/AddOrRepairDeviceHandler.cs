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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

#nullable enable

namespace Hspi.Pages
{
    internal static class AddOrRepairDeviceHandler
    {
        public static (string, bool) PostBackProc(string data,
                                                  IHsController hsController,
                                                  CancellationToken cancellationToken)
        {
            return PostBackProcAsync(data, hsController, cancellationToken).ResultForSync();
        }

        private static async ValueTask<string> Discover(CancellationToken cancellationToken)
        {
            var discoveredDevices = await HomeKitDiscover.DiscoverIPs(TimeSpan.FromSeconds(5), cancellationToken);
            var unPairedDevices = discoveredDevices.Where(x => (x.Status & DeviceStatus.NotPaired) == DeviceStatus.NotPaired);
            var result = new Result { Data = unPairedDevices };
            return JsonConvert.SerializeObject(result);
        }

        private static async ValueTask<string> PairAndCreateDevices(IHsController hsController,
                                                                    JObject requestObject,
                                                                    CancellationToken cancellationToken)
        {
            var pincode = requestObject["pincode"]?.ToString()?.Trim();

            var discoveredDevice = requestObject["data"]?.ToObject<DiscoveredDevice>();
            var existingRefId = (int?)requestObject["refId"];

            if ((pincode == null) || (discoveredDevice == null))
            {
                throw new ArgumentException("Invalid data for pairing");
            }

            if (!pinRegEx.IsMatch(pincode))
            {
                throw new ArgumentException("Invalid pincode format");
            }

            var pairingInfo = await InsecureConnection.StartNewPairing(discoveredDevice, pincode, cancellationToken).ConfigureAwait(false);
            using SecureConnection secureConnection = new(pairingInfo);

            await secureConnection.ConnectAndListen(discoveredDevice.Address, cancellationToken).ConfigureAwait(false);

            var accessoryInfo = secureConnection.DeviceReportedInfo;
            if (existingRefId.HasValue && existingRefId.Value != -1)
            {
                HsHomeKitDeviceFactory.RepairDevice(hsController,
                                                    existingRefId.Value,
                                                    pairingInfo,
                                                    discoveredDevice.Address,
                                                    accessoryInfo);

                Log.Information("Repaired {refId} for {name}", existingRefId.Value, discoveredDevice.DisplayName);
            }
            else
            {
                var accessory1Aid = accessoryInfo?.Accessories.FirstOrDefault(x => x.Aid == Accessory.MainAid);
                if (accessory1Aid == null)
                {
                    Log.Error("No Aid 1 accessor found for {name}", discoveredDevice.DisplayName);
                    throw new InvalidOperationException(Invariant($"No Aid 1 accessory found for {discoveredDevice.DisplayName}"));
                }

                int refId = HsHomeKitDeviceFactory.CreateDevice(hsController,
                                                                pairingInfo,
                                                                discoveredDevice.Address,
                                                                accessory1Aid);

                Log.Information("Created {refId} for {name}", refId, discoveredDevice.DisplayName);
            }

            var result = new Result();
            return JsonConvert.SerializeObject(result);
        }

        private static async Task<(string, bool)> PostBackProcAsync(string data,
                                                                    IHsController hsController,
                                                                    CancellationToken cancellationToken)
        {
            try
            {
                var requestObject = JsonConvert.DeserializeObject<JObject>(data);
                var action = requestObject["action"]?.ToString();
                return action switch
                {
                    "search" => (await Discover(cancellationToken).ConfigureAwait(false), false),
                    "pair" => (await PairAndCreateDevices(hsController, requestObject, cancellationToken).ConfigureAwait(false), true),
                    _ => throw new ArgumentException("Unknown Action"),
                };
            }
            catch (Exception ex)
            {
                var result = new Result { ErrorMessage = ex.GetFullMessage() };
                return (JsonConvert.SerializeObject(result), false);
            }
        }

        public const string PageName = "AddOrRepairDevice.html";
        private static Regex pinRegEx = new Regex(@"^\d\d\d-\d\d-\d\d\d$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromSeconds(60));
        private sealed record Result(string? ErrorMessage = null, object? Data = null);
    }
}