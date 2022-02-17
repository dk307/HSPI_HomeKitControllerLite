using HomeKit;
using Hspi.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
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
                        var discoveredDevices = await HomeKitDiscover.DiscoverIPs(TimeSpan.FromSeconds(5), cancellationToken);
                        var result = new Result { Data = discoveredDevices };
                        return JsonConvert.SerializeObject(result);
                }
            }
            catch (Exception ex)
            {
                var result = new Result { ErrorMessage = ex.GetFullMessage() };
                return JsonConvert.SerializeObject(result);
            }

            return string.Empty;
        }

        public const string PageName = "AddDevice.html";
    }
}