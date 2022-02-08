using HomeKit.Model;
using Newtonsoft.Json;
using System;

#nullable enable

namespace HomeKit.Utils
{

    public sealed class CharacteristicTypeJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(CharacteristicType);
        }

        public override void WriteJson(JsonWriter writer,
                                       object? value, JsonSerializer serializer)
        {
            if (value != null)
            {
                var obj = (CharacteristicType)value;
                writer.WriteValue(obj.Id.ToString("D"));
            }
            else
            {
                writer.WriteNull();
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType,
                                        object existingValue, JsonSerializer serializer)
        {
            var str = (string)reader.Value;
            return new CharacteristicType(str);
        }
    }
}