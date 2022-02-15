using Newtonsoft.Json;
using System.Collections.Immutable;
using System.Linq;

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

        public Characteristic? FindCharacteristic(ulong aid, ulong iid)
        {
            return Accessories.FirstOrDefault(x => x.Aid == aid)?.FindCharacteristic(iid);
        }
    }
}