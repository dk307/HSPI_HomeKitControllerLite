using HomeKit;
using HomeSeer.PluginSdk;
using Hspi.DeviceData;
using Hspi.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Hspi.Pages
{
    internal static class UnpairDeviceHandler
    {
        public static (string, bool) PostBackProc(string data,
                                                  IHsController hsController,
                                                  Task<HsHomeKitDeviceManager> hsHomeKitDeviceManagerFtn,
                                                  CancellationToken cancellationToken)
        {
            return PostBackProcAsync(data, hsController, hsHomeKitDeviceManagerFtn, cancellationToken).ResultForSync();
        }

        private sealed record Result(string? ErrorMessage = null, object? Data = null);

        private static async Task<(string, bool)> PostBackProcAsync(string data,
                                                                    IHsController hsController,
                                                                    Task<HsHomeKitDeviceManager> hsHomeKitDeviceManagerFtn,
                                                                    CancellationToken cancellationToken)
        {
            string? action = null;
            try
            {
                var requestObject = JsonConvert.DeserializeObject<JObject>(data);
                action = requestObject["action"]?.ToString();
                return action switch
                {
                    "unpair" => (await UnpairDevice(hsController,
                                                    hsHomeKitDeviceManagerFtn,
                                                    requestObject,
                                                    cancellationToken).ConfigureAwait(false), true),
                    _ => throw new ArgumentException("Unknown Action"),
                };
            }
            catch (Exception ex)
            {
                string errorMessage = ex.GetFullMessage();
                Log.Error("Operation {action} failed with {error}", action, errorMessage);
                var result = new Result { ErrorMessage = errorMessage };
                return (JsonConvert.SerializeObject(result), false);
            }
        }

        private static async ValueTask<string> UnpairDevice(IHsController hsController,
                                                            Task<HsHomeKitDeviceManager> hsHomeKitDeviceManagerFtn,
                                                            JObject requestObject,
                                                            CancellationToken cancellationToken)
        {
            var refId = (int?)requestObject["data"] ?? throw new ArgumentException("Invalid data for unpairing");

            HsHomeKitBaseRootDevice device = new(hsController, refId);

            // try already connected connection
            try
            {
                var hsHomeKitDeviceManager = await hsHomeKitDeviceManagerFtn.ConfigureAwait(false);
                if (hsHomeKitDeviceManager.Devices.TryGetValue(refId, out var homeKitDevice))
                {
                    await homeKitDevice.Unpair(cancellationToken).ConfigureAwait(false);
                    return JsonConvert.SerializeObject(new Result());
                }
            }
            catch (Exception ex) when (!ex.IsCancelException())
            {
                Log.Warning("Failed to unpair {name} with {error} using existing connection", device.NameForLog, ex.GetFullMessage());
            }

            // try new connection
            using SecureConnection secureConnection = new(device.PairingInfo);
            await secureConnection.ConnectAndListen(device.FallBackAddress, cancellationToken).ConfigureAwait(false);
            await secureConnection.RemovePairing(cancellationToken).ConfigureAwait(false);
            return JsonConvert.SerializeObject(new Result());
        }

        public const string PageName = "UnpairDevice.html";
    }
}