using HomeKit.Model;
using System.Threading;
using System.Threading.Tasks;

#nullable enable


namespace HomeKit
{
    internal sealed class InsecureConnection : Connection
    {
        public InsecureConnection(Device deviceInformation)
            : base(deviceInformation)
        {
        }

        public async Task<PairingDeviceInfo> StartNewPairing(string pin,
                                                             CancellationToken cancellationToken)
        {
            var pairing = new Pairing(this);
            return await pairing.StartNewPairing(pin, cancellationToken).ConfigureAwait(false);
        }
    }
}