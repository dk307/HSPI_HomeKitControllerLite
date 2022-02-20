using HomeSeer.PluginSdk.Devices.Identification;
using Newtonsoft.Json;
using System;

#nullable enable

namespace Hspi.DeviceData.HSMapping
{
    public sealed record HSMappingDeviceType
    {
        public HSMappingDeviceType(Guid serviceIId,
                                   EFeatureType featureType,
                                   int featureSubType)
        {
            ServiceIId = serviceIId;
            FeatureType = featureType;
            FeatureSubType = featureSubType;
        }

        [JsonProperty("serviceiid", Required = Required.Always)]
        public Guid ServiceIId { get; init; }

        [JsonProperty("featureType", Required = Required.Always)]
        public EFeatureType FeatureType { get; init; }

        [JsonProperty("featureSubType", Required = Required.Always)]
        public int FeatureSubType { get; init; }
    }
}