using HomeKit.Model;
using Newtonsoft.Json;
using System;

#nullable enable

namespace HomeKit.Utils
{
    public sealed class ServiceTypeJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ServiceType);
        }

        public override void WriteJson(JsonWriter writer,
                                       object? value, JsonSerializer serializer)
        {
            if (value != null)
            {
                var obj = (ServiceType)value;
                writer.WriteValue(obj.Id.ToString("D"));
            }
            else
            {
                writer.WriteNull();
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType,
                                        object? existingValue, JsonSerializer serializer)
        {
            var str = (string?)reader.Value ?? throw new JsonReaderException();
            return new ServiceType(str);
        }
    }
}