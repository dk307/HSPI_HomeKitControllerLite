using HomeKit.Http;
using NSec.Cryptography;
using System;
using System.Buffers.Binary;
using System.IO;


#nullable enable


namespace HomeKit
{
    public sealed class ChaChaWriteTransform : IWriteTransform, IDisposable
    {
        public ChaChaWriteTransform(ReadOnlySpan<byte> key)
        {
            this.key = Key.Import(chaCha20Poly1305, key, KeyBlobFormat.RawSymmetricKey);
        }

        public void Dispose()
        {
            this.key.Dispose();
        }

        public byte[] Transform(ReadOnlySpan<byte> data)
        {
            MemoryStream memoryStream = new();

            int length = 0;

            const int Chunk = 1024;
            int offset = 0;

            while (offset < data.Length)
            {
                var slice = data.Slice(offset, Math.Min(Chunk, data.Length - offset));

                var lenBytes = new byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(lenBytes, (UInt16)slice.Length);
                var counterBytes = new byte[8];
                BinaryPrimitives.WriteUInt64LittleEndian(counterBytes, counter);
                counter++;

                var nonce = new Nonce(new byte[] { 0, 0, 0, 0 }, counterBytes);
                var result = chaCha20Poly1305.Encrypt(key, nonce, lenBytes, slice);
                memoryStream.Write(lenBytes, 0, lenBytes.Length);
                memoryStream.Write(result, 0, result.Length);
                length += (lenBytes.Length + result.Length);
                offset += slice.Length;
            }
            return memoryStream.ToArray();
        }

        private readonly ChaCha20Poly1305 chaCha20Poly1305 = AeadAlgorithm.ChaCha20Poly1305;
        private readonly Key key;
        private UInt64 counter = 0;
    }
}