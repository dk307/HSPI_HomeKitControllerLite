using Newtonsoft.Json;

namespace HomeKit.Model
{
    internal sealed record AccessoryError
    {
        [JsonProperty("status")]
        public HAPStatus Status;
    }
}