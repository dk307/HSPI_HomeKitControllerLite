using HomeSeer.PluginSdk.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace HSPI_HomeKitControllerTest
{
    internal sealed class PlugExtraDataConverter : JsonConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            PlugExtraData plugExtraData = new();
            JArray jArray = JArray.Load(reader);

            foreach (var value in jArray)
            {
                JObject jObject = (JObject)value;
                plugExtraData.AddNamed((string)jObject["key"], (string)jObject["value"]);
            }

            return plugExtraData;
        }

        public override void WriteJson(JsonWriter writer,
                                       object? value, JsonSerializer serializer)
        {
            JArray jArray = new JArray();
            if (value is PlugExtraData plugExtraData)
            {
                foreach (var item in plugExtraData.NamedKeys)
                {
                    JObject jToken = new JObject();

                    jToken.Add("key", new JValue(item));
                    jToken.Add("value", new JValue(plugExtraData[item]));
                    jArray.Add(jToken);
                }
            }
            jArray.WriteTo(writer);
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(PlugExtraData).IsAssignableFrom(objectType);
        }
    }
}