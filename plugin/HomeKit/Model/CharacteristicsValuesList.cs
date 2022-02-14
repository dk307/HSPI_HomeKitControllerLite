using Newtonsoft.Json;
using System.Collections.Immutable;

#nullable enable

namespace HomeKit.Model
{
    internal sealed record CharacteristicsValuesList
    {
        public CharacteristicsValuesList(IImmutableList<AidIidValue> values)
        {
            Values = values;
        }

        [JsonProperty("characteristics", Required = Required.Always)]
        public IImmutableList<AidIidValue> Values { get; init; }
    }
}