using HomeKit.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace HomeKit.Utils
{
    internal sealed class CharacteristicListConverter : JsonConverter<IImmutableDictionary<ulong, Characteristic>>
    {
        public override IImmutableDictionary<ulong, Characteristic> ReadJson(JsonReader reader,
                                                                             Type objectType,
                                                                             [AllowNull] IImmutableDictionary<ulong, Characteristic> existingValue,
                                                                             bool hasExistingValue,
                                                                             JsonSerializer serializer)
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
                                       [AllowNull] IImmutableDictionary<ulong, Characteristic> value,
                                       JsonSerializer serializer)
        {
            writer.WriteStartArray();
            if (value != null)
            {
                foreach (var entry in value)
                {
                    serializer.Serialize(writer, entry.Value);
                }
            }
            writer.WriteEndArray();
        }
    }
}