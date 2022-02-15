namespace HomeKit.Http
{
    internal interface IReadTransform
    {
        void Transform(ByteBufferWithIndex inputBuffer, ByteBufferWithIndex output);
    }
}