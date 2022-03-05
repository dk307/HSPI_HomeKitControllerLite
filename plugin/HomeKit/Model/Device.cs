using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace HomeKit.Model
{
    internal record Device
    {
        public Device(string id,
                      string displayName,
                      string model,
                      DeviceCategory categoryIdentifier,
                      ushort configurationNumber,
                      DeviceFeature feature,
                      Version protocol)
        {
            Id = id;
            DisplayName = displayName;
            Model = model;
            CategoryIdentifier = categoryIdentifier;
            ConfigurationNumber = configurationNumber;
            Feature = feature;
            Protocol = protocol;
        }

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