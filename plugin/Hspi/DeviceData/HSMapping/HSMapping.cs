using Newtonsoft.Json;
using System;
using System.Collections.Immutable;
using System.ComponentModel;

#nullable enable

namespace Hspi.DeviceData.HSMapping
{

    public sealed record HSMapping
    {
        public HSMapping(Guid iid,
                         string? name,
                         bool forceButtonOptions,
                         ImmutableArray<HSMappingDeviceType>? deviceTypes,
                         RangeOptions? rangeOptions,
                         ImmutableArray<ButtonOption>? buttonOptions)
        {
            Iid = iid;
            Name = name;
            ForceButtonOptions = forceButtonOptions;
            DeviceTypes = deviceTypes;
            RangeOptions = rangeOptions;
            ButtonOptions = buttonOptions;
        }

        [JsonProperty("iid", Required = Required.Always)]
        public Guid Iid { get; init; }

        [JsonProperty("name")]
        public string? Name { get; init; }


        [JsonProperty("forceButtonOptions", DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool ForceButtonOptions { get; init; }

        [JsonProperty("deviceType")]
        public ImmutableArray<HSMappingDeviceType>? DeviceTypes { get; init; }

        [JsonProperty("rangeOptions")]
        public RangeOptions? RangeOptions { get; init; }

        [JsonProperty("buttonOptions")]
        public ImmutableArray<ButtonOption>? ButtonOptions { get; init; }
    }
}