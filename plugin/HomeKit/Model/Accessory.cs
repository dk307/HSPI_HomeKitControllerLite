using HomeKit.Utils;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

#nullable enable

namespace HomeKit.Model
{
    internal sealed record Accessory
    {
        public const ulong MainAid = 1;
        public Accessory(ulong aid, IImmutableDictionary<ulong, Service> services)
        {
            Aid = aid;
            Services = services;
        }

        [JsonProperty("aid", Required = Required.Always)]
        public ulong Aid { get; init; }

        [JsonProperty("services")]
        [JsonConverter(typeof(ServiceListConverter))]
        public IImmutableDictionary<ulong, Service> Services { get; init; }

        [JsonIgnore]
        public string? Name => FindCharacteristic(ServiceType.AccessoryInformation, CharacteristicType.Name)?.Value?.ToString();
        [JsonIgnore]
        public string? Version => FindCharacteristic(ServiceType.AccessoryInformation, CharacteristicType.Version)?.Value?.ToString();
        [JsonIgnore]
        public string? Model => FindCharacteristic(ServiceType.AccessoryInformation, CharacteristicType.Model)?.Value?.ToString();
        [JsonIgnore]
        public string? SerialNumber => FindCharacteristic(ServiceType.AccessoryInformation, CharacteristicType.SerialNumber)?.Value?.ToString();
        [JsonIgnore]
        public string? FirmwareRevision => FindCharacteristic(ServiceType.AccessoryInformation, CharacteristicType.FirmwareRevision)?.Value?.ToString();

        public Characteristic? FindCharacteristic(ServiceType serviceType, CharacteristicType characteristicType)
        {
            return Services?.Values.FirstOrDefault(x => x.Type == serviceType)
                    ?.Characteristics.Values.FirstOrDefault(x => x.Type == characteristicType);
        }

        public (Service?, Characteristic?) FindCharacteristic(ulong iid)
        {
            foreach (var service in Services.Values)
            {
                if (service.Characteristics.TryGetValue(iid, out var value))
                {
                    return (service, value);
                }
            }

            return (null, null);
        }

        public IEnumerable<Characteristic> GetAllReadableCharacteristics()
        {
            return Services.Values.SelectMany(x => x.GetAllReadableCharacteristics());
        }
    }
}