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
        [Unit("°")]
        ArcDegrees,

        [EnumMember(Value = "lux")]
        [Unit("lux")]
        Lux,

        [EnumMember(Value = "seconds")]
        [Unit("s")]
        Seconds,
    }
}