using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HomeKit.Http
{
    internal sealed class ByteBufferWithIndex
    {
        public ByteBufferWithIndex(int size)
        {
            this.buffer = new byte[size];
        }

        public int Length => index;
        public bool IsEmpty => index == 0;

        public void AddToBack(byte[] data)
        {
            if (index + data.Length > buffer.Length)
            {
                Array.Resize(ref buffer, index + data.Length * 2);
            }
            Buffer.BlockCopy(data, 0, buffer, index, data.Length);
            index += data.Length;
        }

        public byte[] RemoveFromFront(int length)
        {
            if (length > index)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            byte[] data = new byte[length];
            Buffer.BlockCopy(buffer, 0, data, 0, length);
            Buffer.BlockCopy(buffer, length, buffer, 0, buffer.Length - length);
            index -= length;
            Debug.Assert(index >= 0);
            return data;
        }

        public async ValueTask ReadFromStream(INetworkReadStream stream, int count,
                                         CancellationToken cancellationToken)
        {
            if (buffer.Length < (index + count))
            {
                Array.Resize(ref buffer, index + count * 2);
            }

            await ReadFromStream(stream, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask ReadFromStream(INetworkReadStream stream, CancellationToken cancellationToken)
        {
            index += await stream.ReadAsync(buffer,
                                            index,
                                            buffer.Length - index,
                                            cancellationToken).ConfigureAwait(false);
        }

        public ReadOnlySpan<byte> AsSpan(int start, int length)
        {
            Debug.Assert(length <= index);
            return buffer.AsSpan(start, length);
        }

        public ReadOnlySpan<byte> AsSpan() => buffer.AsSpan(0, index);

        public ReadOnlyMemory<byte> AsMemory() => new(buffer, 0, index);

        public ReadOnlyMemory<byte> AsMemory(int start, int length)
        {
            Debug.Assert(length <= index);
            return buffer.AsMemory(start, length);
        }

        private byte[] buffer;
        private int index = 0;
    }
}