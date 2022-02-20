using Newtonsoft.Json;
using System.Collections.Immutable;

#nullable enable

namespace Hspi.DeviceData.HSMapping
{
    public sealed record ButtonOption
    {
        public ButtonOption(double value,
                            string name,
                            string? icon,
                            ImmutableArray<EControlUseType>? eControlUses)
        {
            Value = value;
            Name = name;
            Icon = icon;
            EControlUses = eControlUses;
        }

        [JsonProperty("icon")]
        public string? Icon { get; init; }

        [JsonProperty("name", Required = Required.Always)]
        public string Name { get; init; }

        [JsonProperty("name", Required = Required.Always)]
        public double Value { get; init; }

        [JsonProperty("eControlUse")]
        public ImmutableArray<EControlUseType>? EControlUses { get; init; }
    }
}