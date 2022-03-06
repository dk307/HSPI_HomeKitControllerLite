using System.Runtime.Serialization;

#nullable enable

namespace HomeKit.Model
{
    public enum CharacteristicFormat
    {
        [EnumMember(Value = "bool")]
        Bool,

        [EnumMember(Value = "uint8")]
        UnsignedInt8,

        [EnumMember(Value = "uint16")]
        UnsignedInt16,

        [EnumMember(Value = "uint32")]
        UnsignedInt32,

        [EnumMember(Value = "uint64")]
        UnsignedInt64,

        [EnumMember(Value = "int")]
        Integer,

        [EnumMember(Value = "float")]
        Float,

        [EnumMember(Value = "string")]
        String,

        [EnumMember(Value = "tlv8")]
        Tlv8,

        [EnumMember(Value = "data")]
        DataBlob,
    }
}