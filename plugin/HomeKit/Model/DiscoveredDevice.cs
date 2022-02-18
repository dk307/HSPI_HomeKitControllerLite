using HomeKit.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using Zeroconf;

#nullable enable

namespace HomeKit.Model
{
    internal sealed record DiscoveredDevice : Device
    {
        public DiscoveredDevice(string id,
                                string displayName,
                                string model,
                                DeviceCategory categoryIdentifier,
                                ushort configurationNumber,
                                DeviceFeature feature,
                                Version protocol,
                                DeviceStatus status,
                                IPEndPoint address) :
            base(id, displayName, model, categoryIdentifier, configurationNumber, feature, protocol)
        {
            Status = status;
            Address = address;
        }

        public static DiscoveredDevice FromZeroConfigHost(IZeroconfHost zeroconfHost)
        {
            var displayName = zeroconfHost.DisplayName;
            var hapKey = zeroconfHost.Services.Keys.FirstOrDefault(x => x.EndsWith(DiscoveredDevice.HapProtocol));
            var service = zeroconfHost.Services[hapKey];
            var address = new IPEndPoint(IPAddress.Parse(zeroconfHost.IPAddress), service.Port);

            if (service.Properties.Count == 0)
            {
                throw new ArgumentException(null, nameof(zeroconfHost));
            }

            var properties = service.Properties[0];

            return new DiscoveredDevice(
                    ParseProperty(properties, "id") ?? throw new ArgumentException($"id not found for {displayName}"),
                    displayName,
                    ParseProperty(properties, "md") ?? throw new ArgumentException($"md not found for {displayName}"),
                    ParseProperty<DeviceCategory>(properties, "ci") ?? throw new ArgumentException($"ci not found for {displayName}"),
                    ParseProperty<ushort>(properties, "c#") ?? throw new ArgumentException($"c# not found for {displayName}"),
                    ParseProperty<DeviceFeature>(properties, "ff") ?? DeviceFeature.None,
                    Version.Parse(ParseProperty(properties, "pv") ?? "1.0"),
                    ParseProperty<DeviceStatus>(properties, "sf") ?? DeviceStatus.None,
                    address
                );
        }

        public DeviceStatus Status { get; init; }

        [property: JsonConverter(typeof(IPEndPointJsonConverter))]
        public IPEndPoint Address { get; init; }

        private static T? ParseProperty<T>(IReadOnlyDictionary<string, string> properties, string key) where T : struct
        {
            if (properties.TryGetValue(key, out var valueString))
            {
                var converter = TypeDescriptor.GetConverter(typeof(T));

                return (T)(converter.ConvertFromInvariantString(valueString));
            }
            return null;
        }

        private static string? ParseProperty(IReadOnlyDictionary<string, string> properties, string key)
        {
            if (properties.TryGetValue(key, out var valueString))
            {
                return valueString;
            }
            return null;
        }

        public const string HapProtocol = "_hap._tcp.local.";
    }
}