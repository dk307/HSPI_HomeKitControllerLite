using HomeKit.Exceptions;
using HomeKit.Http;
using NSec.Cryptography;
using System;
using System.Buffers.Binary;
using System.Diagnostics;

#nullable enable

namespace HomeKit
{
    internal sealed class ChaChaReadTransform : IReadTransform
    {
        public ChaChaReadTransform(ReadOnlySpan<byte> key)
        {
            this.key = Key.Import(chaCha20Poly1305, key, KeyBlobFormat.RawSymmetricKey);
        }

        public void Transform(ByteBufferWithIndex inputBuffer,
                              ByteBufferWithIndex output)
        {
            while (inputBuffer.Length > 2)
            {
                var lenBytes = inputBuffer.AsSpan(0, 2);
                var blockLength = BinaryPrimitives.ReadInt16LittleEndian(lenBytes);
                var expectedLength = blockLength + 2 + 16;

                if (inputBuffer.Length < expectedLength)
                {
                    // Not enough data yet
                    return;
                }

                var blockAndTag = inputBuffer.AsSpan(2, blockLength + 16);

                var counterBytes = new byte[8];
                BinaryPrimitives.WriteUInt64LittleEndian(counterBytes, counter);

                var nonce = new Nonce(new byte[] { 0, 0, 0, 0 }, counterBytes);
                if (!chaCha20Poly1305.Decrypt(key, nonce, lenBytes,
                                              blockAndTag, out var plainData) 
                    || plainData == null)
                {
                    throw new DecryptionFailedException("Decryption failed from Accessory");
                }

                Debug.Assert(plainData.Length == blockLength);
                counter++;

                inputBuffer.RemoveFromFront(expectedLength);
                output.AddToBack(plainData);
            }
        }

        private readonly ChaCha20Poly1305 chaCha20Poly1305 = AeadAlgorithm.ChaCha20Poly1305;
        private readonly Key key;
        private UInt64 counter = 0;
    }
}