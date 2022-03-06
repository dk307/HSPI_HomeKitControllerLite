using HomeKit.Utils;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace HomeKit.Model
{
    internal sealed record Service
    {
        public Service(ulong iid,
                       ServiceType type,
                       bool? primary,
                       bool? hidden,
                       IImmutableDictionary<ulong, Characteristic> characteristics)
        {
            Iid = iid;
            Type = type;
            Primary = primary;
            Hidden = hidden;
            Characteristics = characteristics;
        }

        [JsonProperty("iid", Required = Required.Always)]
        public ulong Iid { get; init; }

        [JsonProperty("type")]
        [JsonConverter(typeof(ServiceTypeJsonConverter))]
        public ServiceType Type { get; init; }

        [JsonProperty("primary")]
        public bool? Primary { get; init; }

        [JsonProperty("hidden")]
        public bool? Hidden { get; init; }

        [JsonProperty("characteristics")]
        [JsonConverter(typeof(CharacteristicListConverter))]
        public IImmutableDictionary<ulong, Characteristic> Characteristics { get; init; }

        public IEnumerable<Characteristic> GetAllReadableCharacteristics()
        {
            return Characteristics.Values.Where(c => c.Permissions.Contains(CharacteristicPermissions.PairedRead));
        }
    }
}