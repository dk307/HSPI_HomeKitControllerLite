using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;

#nullable enable

namespace HomeKit.Utils
{
    public sealed class IPEndPointJsonConverter : JsonConverter<IPEndPoint>
    {
        public override IPEndPoint ReadJson(JsonReader reader,
                                            Type objectType,
                                            [AllowNull] IPEndPoint existingValue,
                                            bool hasExistingValue,
                                            JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            var addressStr = (string?)jo["Address"];
            var port = (int?)jo["Port"];

            if ((addressStr != null) && (port != null))
            {
                IPAddress address = IPAddress.Parse(addressStr);
                return new IPEndPoint(address, port.Value);
            }
            else
            {
                throw new JsonReaderException();
            }
        }

        public override void WriteJson(JsonWriter writer, IPEndPoint? value, JsonSerializer serializer)
        {
            if (value != null)
            {
                JObject jo = new();
                jo.Add("Address", JToken.FromObject(value.Address.ToString(), serializer));
                jo.Add("Port", value.Port);
                jo.WriteTo(writer);
            }
            else
            {
                writer.WriteNull();
            }
        }
    }
}