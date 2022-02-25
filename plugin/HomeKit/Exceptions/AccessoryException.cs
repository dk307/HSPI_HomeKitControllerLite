using HomeKit.Model;
using System;
using System.Runtime.Serialization;

namespace HomeKit.Exceptions
{


    [Serializable]
    public class AccessoryException : Exception
    {
        public HAPStatus? Status { get;  }

        public AccessoryException()
        {
        }

        public AccessoryException(HAPStatus status)
        {
            this.Status = status;
        }

        public AccessoryException(string message) : base(message)
        {
        }

        public AccessoryException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected AccessoryException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}