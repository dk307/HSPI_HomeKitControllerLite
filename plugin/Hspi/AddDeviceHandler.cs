using HomeKit;
using HomeKit.Model;
using HomeSeer.PluginSdk;
using Hspi.DeviceData;
using Hspi.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
                string? action = (string)requestObject["action"];
                switch (action)
                {
                    case "search":
                        return await Discover(cancellationToken).ConfigureAwait(false);

                    case "pair":
                        return await Pair(hsController, requestObject, cancellationToken).ConfigureAwait(false);

                    default:
                        throw new ArgumentException("Unknown Action");
                }
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
            var pincode = (string)requestObject["pincode"];
            var discoveredDevice = requestObject["data"].ToObject<DiscoveredDevice>();
            var pairingInfo = await InsecureConnection.StartNewPairing(discoveredDevice, pincode, cancellationToken).ConfigureAwait(false);

            using SecureConnection secureConnection = new(pairingInfo);

            await secureConnection.ConnectAndListen(discoveredDevice.Address, cancellationToken).ConfigureAwait(false);

            var accessoryInfo = secureConnection.DeviceReportedInfo;

            HomeKitDeviceFactory.CreateHsDevice(hsController,
                                                pairingInfo,
                                                discoveredDevice.Address,
                                                accessoryInfo.Accessories[0]);

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