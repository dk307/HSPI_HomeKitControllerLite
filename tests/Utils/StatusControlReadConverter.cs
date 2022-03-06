using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace HSPI_HomeKitControllerTest
{
    internal sealed class StatusControlReadConverter : JsonConverter
    {
        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType)
        {
            return typeof(StatusControl).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader,
                                        Type objectType,
                                        object existingValue,
                                        JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);

            var controlType = (int)jObject["ControlType"];
            var graphic = new StatusControl((EControlType)controlType);

            var isRange = (bool)jObject["IsRange"];

            if (isRange)
            {
                JToken jToken = jObject["TargetRange"];
                var min = (double)jToken["Min"];
                var max = (double)jToken["Max"];

                ValueRange valueRange = new(min, max);
                valueRange.Offset = (double)jToken["Offset"];
                valueRange.DecimalPlaces = (int)jToken["DecimalPlaces"];
                valueRange.Prefix = (string)jToken["Prefix"];
                valueRange.Suffix = (string)jToken["Suffix"];

                graphic.TargetRange = valueRange;
            }
            else
            {
                graphic.TargetValue = (double)jObject["TargetValue"];
            }

            graphic.Label = (string)jObject["Label"];
            graphic.ControlUse = (EControlUse)(int)jObject["ControlUse"];
            graphic.HasAdditionalData = (bool)jObject["HasAdditionalData"];

            return graphic;
        }

        public override void WriteJson(JsonWriter writer,
                                       object value,
                                       JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}