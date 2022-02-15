using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net;

#nullable enable

namespace HomeKit.Utils
{
    public sealed class IPEndPointJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(IPEndPoint));
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value != null)
            {
                var ep = (IPEndPoint)value;
                JObject jo = new();
                jo.Add("Address", JToken.FromObject(ep.Address.ToString(), serializer));
                jo.Add("Port", ep.Port);
                jo.WriteTo(writer);
            }
            else
            {
                writer.WriteNull();
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
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
    }
}