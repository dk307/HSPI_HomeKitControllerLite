using HomeKit.Model;
using Newtonsoft.Json;
using System;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace HomeKit.Utils
{

    public sealed class CharacteristicTypeJsonConverter : JsonConverter<CharacteristicType>
    {

        public override void WriteJson(JsonWriter writer,
                                       CharacteristicType? value, JsonSerializer serializer)
        {
            if (value != null)
            {
                writer.WriteValue(value.Id.ToString("D"));
            }
            else
            {
                writer.WriteNull();
            }
        }

        public override CharacteristicType ReadJson(JsonReader reader,
                                                    Type objectType,
                                                    [AllowNull] CharacteristicType existingValue,
                                                    bool hasExistingValue,
                                                    JsonSerializer serializer)
        {
            var str = (string?)reader.Value ?? throw new JsonReaderException();
            return new CharacteristicType(str);
        }

     }
}