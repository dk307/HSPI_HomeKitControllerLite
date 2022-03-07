using Newtonsoft.Json;
using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace HomeKit.Model
{
    public sealed record PairingDeviceInfo(DeviceId DeviceInformation,
                                ImmutableArray<byte> AccessoryPairingId,
                                ImmutableArray<byte> AccessoryPublicKey,  // Public Key
                                Guid ControllerPairingId,
                                ImmutableArray<byte> ControllerDevicePrivateKey, // private key
                                ImmutableArray<byte> ControllerDevicePublicKey, // public key
                                bool EnableKeepAliveForConnection,
                                TimeSpan? PollingTimeSpan)
    {
        public byte[] GetControllerPairingIdAsBytes() => EncodeGuid(ControllerPairingId);

        public static byte[] EncodeGuid(Guid id)
        {
            return Encoding.UTF8.GetBytes(id.ToString("D", CultureInfo.InvariantCulture));
        }
    }
}