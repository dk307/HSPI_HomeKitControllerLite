using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

#nullable enable


namespace HomeKit
{
    public enum TlvType : byte
    {
        Method = 0,
        Identifier = 1,
        Salt = 2,
        PublicKey = 3,
        Proof = 4,
        EncryptedData = 5,
        State = 6,
        Error = 7,
        RetryDelay = 8,
        Certificate = 9,
        Signature = 10,
        Permissions = 11,
        Flags = 19,
        Separator = 255, // 0 bytes length always
    }

    internal enum TlvErrorCode
    {
        UNKNOWN = 0x01,
        AUTHENTICATION = 0x02, // setup code or signature verification failed
        BACKOFF = 0x03, // // client must look at retry delay tlv item
        MAX_PEERS = 0x04, // server cannot accept any more pairings
        MAX_TRIES = 0x05, // server reached maximum number of authentication attempts
        UNAVAILABLE = 0x06, // server pairing method is unavailable
        BUSY = 0x07 // cannot accept pairing request at this time
    }

    public sealed record TlvValue
    {
        public TlvValue(TlvType type, IEnumerable<byte> value) :
            this(type, value.ToArray())
        {
        }

        public TlvValue(TlvType type, byte[] value)
        {
            this.Type = type;
            this.Value = value.ToImmutableArray();
        }

        public TlvValue(TlvType type, ImmutableArray<byte> value)
        {
            this.Type = type;
            this.Value = value;
        }

        public TlvType Type { get; init; }
        public ImmutableArray<byte> Value { get; init; }

        public bool Equals(TlvValue other)
        {
            return (Type == other.Type) &&
                  Value.SequenceEqual(other.Value);
        }

        public override int GetHashCode()
        {
            var hashCode = Type.GetHashCode();
            foreach (var item in Value)
            {
                hashCode ^= item.GetHashCode();
            }

            return hashCode;
        }
    }

    public static class Tlv8
    {
        public static ImmutableArray<byte> AddPairing => new byte[] { 3 }.ToImmutableArray();
        public static ImmutableArray<byte> RemovePairing => new byte[] { 4 }.ToImmutableArray();
        public static ImmutableArray<byte> ErrorAuthentication => new byte[] { 2 }.ToImmutableArray();
        public static ImmutableArray<byte> ListPair => new byte[] { 5 }.ToImmutableArray();
        public static ImmutableArray<byte> M1 => new byte[] { 1 }.ToImmutableArray();
        public static ImmutableArray<byte> M2 => new byte[] { 2 }.ToImmutableArray();
        public static ImmutableArray<byte> M3 => new byte[] { 3 }.ToImmutableArray();
        public static ImmutableArray<byte> M4 => new byte[] { 4 }.ToImmutableArray();
        public static ImmutableArray<byte> M5 => new byte[] { 5 }.ToImmutableArray();
        public static ImmutableArray<byte> M6 => new byte[] { 6 }.ToImmutableArray();
        public static ImmutableArray<byte> PairSetup => new byte[] { 0 }.ToImmutableArray();
        public static ImmutableArray<byte> PairSetupWithAuth => new byte[] { 1 }.ToImmutableArray();
        public static ImmutableArray<byte> PairVerify => new byte[] { 2 }.ToImmutableArray();
        public static ImmutableArray<byte> PermissionRegularAdmin => new byte[] { 1 }.ToImmutableArray();
        public static ImmutableArray<byte> PermissionRegularUser => new byte[] { 0 }.ToImmutableArray();

        public static IEnumerable<TlvValue> Decode(byte[] data)
        {
            var stream = new MemoryStream(data, false)
            {
                Position = 0
            };
            return Decode(stream);
        }

        public static IEnumerable<TlvValue> Decode(Stream data)
        {
            using var reader = new BinaryReader(data);
            while (reader.PeekChar() != -1)
            {
                var type = (TlvType)reader.ReadByte();
                if (type == TlvType.Separator)
                {
                    reader.ReadByte(); // empty length
                    yield return new TlvValue(type, Array.Empty<byte>());
                }
                else
                {
                    var tlvValue = new MemoryStream(byte.MaxValue);
                    while (true)
                    {
                        var currentLength = reader.ReadByte();
                        tlvValue.Write(reader.ReadBytes(currentLength), 0, currentLength);

                        if (!(((byte)type == reader.PeekChar()) && (currentLength == byte.MaxValue)))
                        {
                            break;
                        }
                        else
                        {
                            reader.ReadByte(); // read away type
                        }
                    }
                    yield return new TlvValue(type, tlvValue.ToArray());
                }
            }
            Debug.Assert(reader.PeekChar() == -1);
        }

        public static byte[] Encode(IEnumerable<TlvValue> tlvList)
        {
            var result = new MemoryStream();
            foreach (var pair in tlvList)
            {
                if ((pair.Type == TlvType.Separator) || pair.Value.Length == 0)
                {
                    result.WriteByte((byte)pair.Type);
                    result.WriteByte(0);
                }
                else
                {
                    var data = pair.Value;
                    var length = data.Length;
                    var offset = 0;
                    while (length > 0)
                    {
                        var chunkLength = (byte)Math.Min(byte.MaxValue, length);

                        result.WriteByte((byte)pair.Type);
                        result.WriteByte(chunkLength);
                        for (var i = offset; i < offset + chunkLength; i++)
                        {
                            result.WriteByte(data[i]);
                        }

                        length -= chunkLength;
                        offset += chunkLength;
                    }
                }
            }
            return result.ToArray();
        }
    }
}