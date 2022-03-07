using HomeKit.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

#nullable enable

namespace HomeKit.Model
{
    public record BaseGuidType
    {
        private const string BaseGuid = "-0000-1000-8000-0026BB765291";
        public BaseGuidType(string accessoryValue)
        {
            this.Id = MakeFullGuid(accessoryValue);
        }

        public BaseGuidType(Guid id)
        {
            this.Id = id;
        }

        private static Guid MakeFullGuid(string value)
        {
            if (value.Length <= 8)
            {
                var prefix = new string('0', (8 - value.Length));
                return Guid.Parse($"{prefix}{value}{BaseGuid}");
            }

            // try all formats
            if (Guid.TryParse(value, out var result))
            {
                return result;
            }
            throw new ArgumentException("Invalid value: " + value);
        }

        protected static ImmutableDictionary<Guid, string> CreateDefaultNames(byte[] data)
        {
            string json = Encoding.UTF8.GetString(data);
            var jObject = JsonHelper.DeserializeObject<JObject>(json);

            var result = (jObject["names"] as JArray).Select(x =>
            {
                var idString = (string?)x["id"];
                var name = (string?)x["name"];

                if (!Guid.TryParse(idString, out var id) || name == null)
                {
                    throw new InvalidProgramException("Name map is invalid");
                }

                return new ValueTuple<Guid, string>(id, name);
            }).ToImmutableDictionary(x => x.Item1, x => x.Item2);

            return result;
        }

        public Guid Id { get; init; }
    }
}