using HomeKit.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Immutable;
using System.Linq;

namespace HomeKit.Model
{

    internal sealed record Characteristic
    {
        public Characteristic(uint iid, 
                              CharacteristicType type, 
                              string value, 
                              IImmutableList<CharacteristicPermissions> permissions, 
                              bool? eventNotifications, 
                              string format, 
                              string description, 
                              string unit, 
                              double? minimumValue, 
                              double? maximumValue, 
                              double? stepValue, 
                              int? maximumLength, 
                              int? maxDataLength, 
                              IImmutableList<double> validValues, 
                              IImmutableList<double> validValuesRange, 
                              long? ttl, 
                              long? pid)
        {
            Iid = iid;
            Type = type;
            Value = value;
            Permissions = permissions;
            EventNotifications = eventNotifications;
            Format = format;
            Description = description;
            Unit = unit;
            MinimumValue = minimumValue;
            MaximumValue = maximumValue;
            StepValue = stepValue;
            MaximumLength = maximumLength;
            MaxDataLength = maxDataLength;
            ValidValues = validValues;
            ValidValuesRange = validValuesRange;
            Ttl = ttl;
            Pid = pid;
        }

        [JsonProperty("iid", Required = Required.Always)]
        public uint Iid { get; init; }

        [JsonProperty("type", Required = Required.Always)]
        [JsonConverter(typeof(CharacteristicTypeJsonConverter))]
        public CharacteristicType Type { get; init; }

        [JsonProperty("value")]
        public string Value { get; init; }

        [JsonProperty("perms", Required = Required.Always, ItemConverterType = typeof(StringEnumConverter))]
        public IImmutableList<CharacteristicPermissions> Permissions { get; init; }

        [JsonProperty("ev")]
        public bool? EventNotifications { get; init; }

        [JsonProperty("format", Required = Required.Always)]
        public string Format { get; init; }

        [JsonProperty("description")]
        public string Description { get; init; }
        [JsonProperty("unit")]
        public string Unit { get; init; }

        [JsonProperty("minValue")]
        public double? MinimumValue { get; init; }

        [JsonProperty("maxValue")]
        public double? MaximumValue { get; init; }

        [JsonProperty("minStep")]
        public double? StepValue { get; init; }

        [JsonProperty("maxLen")]
        public int? MaximumLength { get; init; }

        [JsonProperty("maxDataLen")]
        public int? MaxDataLength { get; init; }

        [JsonProperty("valid-values")]
        public IImmutableList<double> ValidValues { get; init; }

        [JsonProperty("valid-values-range")]
        public IImmutableList<double> ValidValuesRange { get; init; }

        [JsonProperty("TTL")]
        public long? Ttl { get; init; }

        [JsonProperty("pid")]
        public long? Pid { get; init; }

        [JsonIgnore]
        public bool SupportsNotifications => Permissions.Contains(CharacteristicPermissions.Events);
    }
}