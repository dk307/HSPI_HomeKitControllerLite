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
        public DiscoveredDevice(IZeroconfHost zeroconfHost)
        {
            this.DisplayName = zeroconfHost.DisplayName;
            var service = zeroconfHost.Services[HapProtocol];
            this.Address = new IPEndPoint(IPAddress.Parse(zeroconfHost.IPAddress), service.Port);

            var properties = service.Properties.FirstOrDefault();
            if (properties == null)
            {
                throw new ArgumentException(null, nameof(zeroconfHost));
            }

            this.ConfigurationNumber = ParseProperty<ushort>(properties, "c#") ?? throw new ArgumentException($"c# not found for {DisplayName}");
            this.CategoryIdentifier = ParseProperty<DeviceCategory>(properties, "ci") ?? throw new ArgumentException($"ci not found for {DisplayName}");
            this.Id = ParseProperty(properties, "id") ?? throw new ArgumentException($"id not found for {DisplayName}");
            this.Model = ParseProperty(properties, "md") ?? throw new ArgumentException($"md not found for {DisplayName}");
            this.Protocol = Version.Parse(ParseProperty(properties, "pv") ?? "1.0");
            this.Feature = ParseProperty<DeviceFeature>(properties, "ff") ?? DeviceFeature.None;
            this.Status = ParseProperty<DeviceStatus>(properties, "sf") ?? DeviceStatus.None;
        }

        public DeviceStatus Status { get; init; }

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