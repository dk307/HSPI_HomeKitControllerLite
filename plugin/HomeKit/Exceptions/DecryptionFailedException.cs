using System;
using System.Runtime.Serialization;

namespace HomeKit.Exceptions
{
    [Serializable]
    public class DecryptionFailedException : Exception
    {
        public DecryptionFailedException()
        {
        }

        public DecryptionFailedException(string message) : base(message)
        {
        }

        public DecryptionFailedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected DecryptionFailedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}