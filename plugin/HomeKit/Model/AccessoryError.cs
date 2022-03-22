using Newtonsoft.Json;

#nullable enable


namespace HomeKit.Model
{
    internal sealed record AccessoryError
    {
        [JsonProperty("status")]
        public HAPStatus Status;
    }
}