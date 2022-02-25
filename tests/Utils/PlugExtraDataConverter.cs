using HomeSeer.PluginSdk.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace HSPI_HomeKitControllerTest
{
    internal sealed class PlugExtraDataConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(PlugExtraData).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader,
                                        Type objectType,
                                        object existingValue,
                                        JsonSerializer serializer)
        {
            JObject jObjectTop = JObject.Load(reader);
            PlugExtraData plugExtraData = new();
            JArray jArray = (JArray)jObjectTop["Values"];

            foreach (var value in jArray)
            {
                JObject jObject = (JObject)value;
                plugExtraData.AddNamed((string)jObject["key"], (string)jObject["value"]);
            }

            return plugExtraData;
        }

        public override void WriteJson(JsonWriter writer,
                                       object value,
                                       JsonSerializer serializer)
        {
            JObject jObject = new();
            if (serializer.TypeNameHandling != TypeNameHandling.None)
            {
                jObject.Add("$type", $"{value.GetType().FullName}, PluginSdk");
            }

            JArray jArray = new();
            if (value is PlugExtraData plugExtraData)
            {
                foreach (var item in plugExtraData.NamedKeys)
                {
                    JObject jToken = new();

                    jToken.Add("key", new JValue(item));
                    jToken.Add("value", new JValue(plugExtraData[item]));
                    jArray.Add(jToken);
                }
            }
            jObject.Add("Values", jArray);
            jObject.WriteTo(writer);
        }
    }
}