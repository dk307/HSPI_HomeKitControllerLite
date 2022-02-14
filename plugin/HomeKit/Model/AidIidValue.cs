using Newtonsoft.Json;

#nullable enable

namespace HomeKit.Model
{
    internal sealed record AidIidValue
    {
        public AidIidValue(ulong aid, ulong iid, object? value)
        {
            Aid = aid;
            Iid = iid;
            Value = value;
        }

        [JsonProperty("aid", Required = Required.Always)]
        public ulong Aid { get; init; }

        [JsonProperty("iid", Required = Required.Always)]
        public ulong Iid { get; init; }

        [JsonProperty("value")]
        public object? Value { get; init; }
    }
}