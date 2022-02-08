using HomeKit.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Net;

namespace HomeKit.Model
{
    internal record Device
    {
        [property: JsonConverter(typeof(IPEndPointJsonConverter))]
        public IPEndPoint Address { get; init; }

        [property: JsonConverter(typeof(StringEnumConverter))]
        public DeviceCategory CategoryIdentifier { get; init; }

        public ushort ConfigurationNumber { get; init; }

        public string DisplayName { get; init; }

        [property: JsonConverter(typeof(StringEnumConverter))]
        public DeviceFeature Feature { get; init; }

        public string Id { get; init; }

        public string Model { get; init; }

        [property: JsonConverter(typeof(VersionConverter))]
        public Version Protocol { get; init; }
    }
}