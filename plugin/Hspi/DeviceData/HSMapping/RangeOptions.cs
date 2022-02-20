using Newtonsoft.Json;
using System.Collections.Immutable;

#nullable enable

namespace Hspi.DeviceData.HSMapping
{
    public sealed record RangeOptions
    {
        public RangeOptions(string? icon,
                            ImmutableArray<EControlUseType>? eControlUses)
        {
            Icon = icon;
            EControlUses = eControlUses;
        }

        [JsonProperty("icon")]
        public string? Icon { get; init; }

        [JsonProperty("eControlUse")]
        public ImmutableArray<EControlUseType>? EControlUses { get; init; }
    }
}