using Newtonsoft.Json;
using System.Collections.Immutable;

#nullable enable

namespace Hspi.DeviceData.HSMapping
{
    public sealed record HSMappings
    {
        [JsonProperty("mappings")]
        public ImmutableArray<HSMapping>? Mappings { get; init; }
    }
}