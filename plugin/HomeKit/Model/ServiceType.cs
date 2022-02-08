using System;

namespace HomeKit.Model
{
    internal sealed record ServiceType : BaseGuidType
    {
        public ServiceType(string value) : base(value)
        {
        }

        public ServiceType(Guid id) : base(id)
        {
        }

        public static readonly ServiceType AccessoryInformation = new("3E");
        public static readonly ServiceType Fan = new("40");
        public static readonly ServiceType Fan2 = new("B7");
        public static readonly ServiceType Thermostat = new("4A");
        public static readonly ServiceType TemperatureSensor = new("8A");
        public static readonly ServiceType MotionSensor = new("85");
        public static readonly ServiceType HumiditySensor = new("82");
    }
}