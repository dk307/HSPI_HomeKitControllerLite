using System.Runtime.Serialization;

namespace HomeKit.Model
{
    public enum CharacteristicPermissions
    {
        [EnumMember(Value = "pr")]
        PairedRead,

        [EnumMember(Value = "pw")]
        PairedWrite,

        [EnumMember(Value = "ev")]
        Events,

        [EnumMember(Value = "aa")]
        AdditionalAuthorization,

        [EnumMember(Value = "tw")]
        TimedWrite,

        [EnumMember(Value = "hd")]
        Hidden,

        [EnumMember(Value = "wr")]
        WriteResponse,
    }
}