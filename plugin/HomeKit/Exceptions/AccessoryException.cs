using HomeKit.Model;
using System;
using System.Runtime.Serialization;

#nullable enable


namespace HomeKit.Exceptions
{
    [Serializable]
    public class AccessoryException : Exception
    {
        public ulong? Aid { get; }
        public ulong? Iid { get; }
        public HAPStatus? Status { get; }

        public AccessoryException()
        {
        }

        public AccessoryException(ulong? aid, ulong? iid, HAPStatus? status)
            : base($"Accessory operation for Aid:{aid} Iid:{iid} failed with {status}")
        {
            Aid = aid;
            Iid = iid;
            this.Status = status;
        }
        public AccessoryException(HAPStatus? status)
            : base($"Accessory operation failed with {status}")
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
            // TODO:: implement this
        }
    }
}