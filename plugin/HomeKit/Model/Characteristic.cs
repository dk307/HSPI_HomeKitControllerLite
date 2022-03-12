using HomeKit.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Immutable;
using System.Linq;

#nullable enable

namespace HomeKit.Model
{
    internal sealed record Characteristic
    {
        public Characteristic(ulong iid,
                              CharacteristicType type,
                              object? value,
                              IImmutableList<CharacteristicPermissions> permissions,
                              bool? eventNotifications,
                              CharacteristicFormat format,
                              string description,
                              CharacteristicUnit? unit,
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
        public ulong Iid { get; init; }

        [JsonProperty("type", Required = Required.Always)]
        [JsonConverter(typeof(CharacteristicTypeJsonConverter))]
        public CharacteristicType Type { get; init; }

        [JsonProperty("value")]
        public object? Value { get; init; }

        [JsonProperty("perms", Required = Required.Always, ItemConverterType = typeof(StringEnumConverter))]
        public IImmutableList<CharacteristicPermissions> Permissions { get; init; }

        [JsonProperty("ev")]
        public bool? EventNotifications { get; init; }

        [JsonProperty("format", Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public CharacteristicFormat Format { get; init; }

        [JsonProperty("description")]
        public string Description { get; init; }

        [JsonProperty("unit")]
        [JsonConverter(typeof(StringEnumConverter))]
        public CharacteristicUnit? Unit { get; init; }

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
        public IImmutableList<double>? ValidValues { get; init; }

        [JsonProperty("valid-values-range")]
        public IImmutableList<double>? ValidValuesRange { get; init; }

        [JsonProperty("TTL")]
        public long? Ttl { get; init; }

        [JsonProperty("pid")]
        public long? Pid { get; init; }

        [JsonIgnore]
        public int? DecimalPlaces => StepValue != null ? GetPrecision((decimal)StepValue) : null;

        private static int GetPrecision(decimal x)
        {
            int precision = 0;
            while (x * (decimal)Math.Pow(10, precision) != Math.Round(x * (decimal)Math.Pow(10, precision)))
            {
                precision++;
            }
            return precision;
        }

        [JsonIgnore]
        public bool SupportsNotifications => Permissions.Contains(CharacteristicPermissions.Events);

        [JsonIgnore]
        public bool IsBooleanFormatType
        {
            get
            {
                if (Format == CharacteristicFormat.Bool)
                {
                    return true;
                }

                if ((Format == CharacteristicFormat.UnsignedInt8) &&
                    (MinimumValue == 0) &&
                    (MaximumValue == 1))
                {
                    return true;
                }

                return false;
            }
        }
    }
}