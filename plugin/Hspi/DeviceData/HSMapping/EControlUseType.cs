using Newtonsoft.Json;
using System;

#nullable enable

namespace Hspi.DeviceData.HSMapping
{
    public sealed record EControlUseType
    {
        public EControlUseType(Guid serviceIId, int value)
        {
            ServiceIId = serviceIId;
            Value = value;
        }

        [JsonProperty("serviceiid", Required = Required.Always)]
        public Guid ServiceIId { get; init; }

        [JsonProperty("value", Required = Required.Always)]
        public int Value { get; init; }
    }
}