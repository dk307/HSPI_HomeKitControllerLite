using HomeKit;
using HomeKit.Model;
using Hspi.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hspi
{
    internal static class AddDeviceHandler
    {
        public static string PostBackProc(string data, CancellationToken cancellationToken)
        {
            return PostBackProcAsync(data, cancellationToken).ResultForSync();
        }

        private sealed record Result(string ErrorMessage = null, object Data = null);

        private static async Task<string> PostBackProcAsync(string data, CancellationToken cancellationToken)
        {
            try
            {
                var requestObject = JsonConvert.DeserializeObject<JObject>(data);
                switch ((string)requestObject["action"])
                {
                    case "search":
                        return await Discover(cancellationToken).ConfigureAwait(false);

                    case "pair":
                        return await Pair(requestObject, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                var result = new Result { ErrorMessage = ex.GetFullMessage() };
                return JsonConvert.SerializeObject(result);
            }

            return string.Empty;
        }

        private static async ValueTask<string> Pair(JObject requestObject, CancellationToken cancellationToken)
        {
            var pincode = (string)requestObject["pincode"];
            var discoveredDevice = requestObject["data"].ToObject<DiscoveredDevice>();
            await InsecureConnection.StartNewPairing(discoveredDevice, pincode, cancellationToken).ConfigureAwait(false);
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