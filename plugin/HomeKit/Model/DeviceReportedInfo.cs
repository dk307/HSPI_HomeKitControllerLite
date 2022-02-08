using Newtonsoft.Json;
using System.Collections.Immutable;

#nullable enable

namespace HomeKit.Model
{
    internal sealed record DeviceReportedInfo
    {
        public DeviceReportedInfo(IImmutableList<Accessory> accessories)
        {
            Accessories = accessories;
        }

        [JsonProperty("accessories")]
        public IImmutableList<Accessory> Accessories { get; init; }
    }
}