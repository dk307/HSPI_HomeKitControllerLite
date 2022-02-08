using System;
using System.Runtime.Serialization;

namespace HomeKit.Exceptions
{
    [Serializable]
    public class AccessoryDisconnectedException : Exception
    {
        public AccessoryDisconnectedException()
        {
        }

        public AccessoryDisconnectedException(string message) : base(message)
        {
        }

        public AccessoryDisconnectedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected AccessoryDisconnectedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}