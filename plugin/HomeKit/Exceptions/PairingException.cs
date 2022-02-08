using System;
using System.Runtime.Serialization;

namespace HomeKit.Exceptions
{
    [Serializable]

    public class PairingException : Exception
    {
        public PairingException()
        {
        }

        public PairingException(string message) : base(message)
        {
        }

        public PairingException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected PairingException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}