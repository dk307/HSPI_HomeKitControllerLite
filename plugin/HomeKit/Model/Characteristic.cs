using HomeKit.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.Serialization;

namespace HomeKit.Model
{
    public enum CharacteristicPermissions
    {
        [EnumMember(Value = "pr")]
        PairedRead,

        [EnumMember(Value = "pw")]
        PairedWrite,

        [EnumMember(Value = "ev")]
        Events,

        [EnumMember(Value = "aa")]
        AdditionalAuthorization,

        [EnumMember(Value = "tw")]
        TimedWrite,

        [EnumMember(Value = "hd")]
        Hidden,

        [EnumMember(Value = "wr")]
        WriteResponse,
    }

    internal sealed record Characteristic
    {
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