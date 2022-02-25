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

        public override bool CanRead => false;

        public override object ReadJson(JsonReader reader,
                                        Type objectType,
                                        object existingValue,
                                        JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer,
                                       object value,
                                       JsonSerializer serializer)
        {
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
            jArray.WriteTo(writer);
        }
    }
}