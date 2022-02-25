using HomeKit.Model;
using Newtonsoft.Json;
using System;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace HomeKit.Utils
{
    public sealed class ServiceTypeJsonConverter : JsonConverter<ServiceType>
    {

        public override ServiceType ReadJson(JsonReader reader,
                                             Type objectType,
                                             [AllowNull] ServiceType existingValue,
                                             bool hasExistingValue,
                                             JsonSerializer serializer)
        {
            var str = (string?)reader.Value ?? throw new JsonReaderException();
            return new ServiceType(str);
        }

        public override void WriteJson(JsonWriter writer,
                                               ServiceType? value, JsonSerializer serializer)
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
     }
}