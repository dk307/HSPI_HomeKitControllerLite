using Hspi;
using System;
using System.Collections.Immutable;

namespace HomeKit.Model
{
    public sealed record ServiceType : BaseGuidType
    {
        public ServiceType(string value) : base(value)
        {
        }

        public ServiceType(Guid id) : base(id)
        {
        }

        private static readonly ImmutableDictionary<Guid, string> defaultNames = CreateDefaultNames(Resource.ServiceTypeNames);

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



        public static readonly ServiceType AccessoryInformation = new("3E");
        public static readonly ServiceType ProtocolInformation = new("A2");
        public static readonly ServiceType Fan = new("40");
        public static readonly ServiceType Fan2 = new("B7");
        public static readonly ServiceType Thermostat = new("4A");
        public static readonly ServiceType TemperatureSensor = new("8A");
        public static readonly ServiceType MotionSensor = new("85");
        public static readonly ServiceType HumiditySensor = new("82");
    }
}