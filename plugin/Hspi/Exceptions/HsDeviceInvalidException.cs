using System;

namespace Hspi.Exceptions
{
    [Serializable]
    public class HsDeviceInvalidException : Exception
    {
        public HsDeviceInvalidException(string message) : base(message)
        {
        }

        public HsDeviceInvalidException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected HsDeviceInvalidException(System.Runtime.Serialization.SerializationInfo serializationInfo, System.Runtime.Serialization.StreamingContext streamingContext)
        {
            throw new NotImplementedException();
        }

        public HsDeviceInvalidException()
        {
        }
    }
}