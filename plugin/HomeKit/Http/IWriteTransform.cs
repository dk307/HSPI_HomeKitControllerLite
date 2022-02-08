using System;

namespace HomeKit.Http
{
    internal interface IWriteTransform
    {
        byte[] Transform(ReadOnlySpan<byte> data);
    }
}