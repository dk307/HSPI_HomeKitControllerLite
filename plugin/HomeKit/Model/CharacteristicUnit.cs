using HomeKit.Utils;
using System.Runtime.Serialization;

#nullable enable

namespace HomeKit.Model
{
    public enum CharacteristicUnit
    {
        [EnumMember(Value = "celsius")]
        [Unit("C")]
        Celsius,

        [EnumMember(Value = "percentage")]
        [Unit("%")]
        Percentage,

        [EnumMember(Value = "arcdegrees")]
        ArcDegrees,

        [EnumMember(Value = "lux")]
        Lux,

        [EnumMember(Value = "seconds")]
        Seconds,
    }
}