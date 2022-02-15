using System;
using System.Runtime.Serialization;

namespace HomeKit.Exceptions
{

    [Serializable]

    public class VerifyPairingException : PairingException
    {
        public VerifyPairingException()
        {
        }

        public VerifyPairingException(string message) : base(message)
        {
        }

        public VerifyPairingException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected VerifyPairingException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}