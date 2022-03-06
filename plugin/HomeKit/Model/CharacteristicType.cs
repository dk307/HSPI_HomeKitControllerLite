#nullable enable

using HomeKit.Utils;
using Hspi;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

#nullable enable

namespace HomeKit.Model
{
    public sealed record CharacteristicType : BaseGuidType
    {
        public CharacteristicType(string value) : base(value)
        {
        }

        private static ImmutableDictionary<Guid, string> CreateDefaultNames()
        {
            string json = Encoding.UTF8.GetString(Resource.CharacteristicTypeNames);
            var jObject = JsonHelper.DeserializeObject<JObject>(json);

            var result = (jObject["names"] as JArray).Select(x =>
            {
                var idString = (string?)x["id"];
                var name = (string?)x["name"];

                if (!Guid.TryParse(idString, out var id) || name == null)
                {
                    throw new InvalidProgramException("CharacteristicType name map is invalid");
                }
 
                return new ValueTuple<Guid, string>(id, name);
            }).ToImmutableDictionary(x => x.Item1, x => x.Item2);

            return result;
        }

        private static readonly ImmutableDictionary<Guid, string> defaultNames = CreateDefaultNames();

        public string? DisplayName
        {
            get
            {
                if (defaultNames.TryGetValue(Id, out var name))
                {
                    return name;
                }
                return null;
            }
        }

        public override string ToString()
        {
            return this.DisplayName ?? Id.ToString();
        }

        public static readonly CharacteristicType Category = new("A3");
        public static readonly CharacteristicType Name = new("23");
        public static readonly CharacteristicType Version = new("37");
        public static readonly CharacteristicType Identify = new("14");
        public static readonly CharacteristicType Manufacturer = new("20");
        public static readonly CharacteristicType Model = new("21");
        public static readonly CharacteristicType SerialNumber = new("30");
        public static readonly CharacteristicType FirmwareRevision = new("52");
        public static readonly CharacteristicType HardwareRevision = new("53");
        public static readonly CharacteristicType ProductData = new("220");
    }
}