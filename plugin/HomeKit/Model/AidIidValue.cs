using Newtonsoft.Json;

#nullable enable

namespace HomeKit.Model
{
    internal record AidIidPair
    {
        public AidIidPair(ulong aid, ulong iid)
        {
            Aid = aid;
            Iid = iid;
        }

        [JsonProperty("aid", Required = Required.Always)]
        public ulong Aid { get; init; }

        [JsonProperty("iid", Required = Required.Always)]
        public ulong Iid { get; init; }
    }

    internal sealed record AidIidValue : AidIidPair
    {
        public AidIidValue(ulong aid, ulong iid, object? value)
            : base(aid, iid)
        {
            Value = value;
        }

        [JsonProperty("value")]
        public object? Value { get; init; }
    }
}