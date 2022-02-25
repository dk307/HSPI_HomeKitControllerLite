using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace HSPI_HomeKitControllerTest
{
    internal sealed class StatusGraphicReadConverter : JsonConverter
    {
        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType)
        {
            return typeof(StatusGraphic).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader,
                                        Type objectType,
                                        object existingValue,
                                        JsonSerializer serializer)
        {
            StatusGraphic graphic = null;
            var jObject = JObject.Load(reader);

            var isRange = (bool)jObject["IsRange"];
            var graphicPath = (string)jObject["Graphic"];

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

                graphic = new StatusGraphic(graphicPath, valueRange);
            }
            else
            {
                var value = (double)jObject["Value"];
                graphic = new StatusGraphic(graphicPath, value);
            }

            graphic.Label = (string)jObject["Label"];
            graphic.ControlUse = (EControlUse)(int)jObject["ControlUse"];
            graphic.HasAdditionalData = (bool)jObject["HasAdditionalData"];

            return graphic;
        }
        public override void WriteJson(JsonWriter writer,
                                       object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}