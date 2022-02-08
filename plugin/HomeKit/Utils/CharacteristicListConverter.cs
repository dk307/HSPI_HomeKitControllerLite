using HomeKit.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

#nullable enable

namespace HomeKit.Utils
{
    internal sealed class CharacteristicListConverter : JsonConverter
    {
        public override object ReadJson(JsonReader reader, 
                                        Type objectType, 
                                        object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                // Load JArray from stream
                var jArray = JArray.Load(reader);

                // Create target object based on JObject
                var list = new List<Characteristic>();

                // Populate the object properties
                serializer.Populate(jArray.CreateReader(), list);
                var dictionary = new Dictionary<ulong, Characteristic>();
                foreach (var item in list)
                {
                    if (!dictionary.ContainsKey(item.Iid))
                    {
                        dictionary.Add(item.Iid, item);
                    }
                }
                return dictionary.ToImmutableDictionary();
            }
            return ImmutableDictionary<ulong, Characteristic>.Empty;
        }

        public override void WriteJson(JsonWriter writer,
                                       object value, JsonSerializer serializer)
        {
            if (value is IImmutableDictionary<ulong, Service> values)
            {
                writer.WriteStartArray();
                foreach (var entry in values)
                {
                    serializer.Serialize(writer, entry.Value);
                }
                writer.WriteEndArray();
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(IImmutableDictionary<ulong, Characteristic>).IsAssignableFrom(objectType);
        }
    }
}