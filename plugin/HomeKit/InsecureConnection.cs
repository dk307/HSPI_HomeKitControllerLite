﻿using HomeKit.Exceptions;
using HomeKit.Model;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace HomeKit
{
    internal sealed class InsecureConnection : Connection
    {
        private InsecureConnection(DeviceId deviceInformation)
            : base(deviceInformation, false)
        {
        }

        public static async Task<PairingDeviceInfo> StartNewPairing(DiscoveredDevice deviceInformation,
                                                                    string pin,
                                                                    CancellationToken cancellationToken)
        {
            using var connection = new InsecureConnection(deviceInformation);
            var pairing = new Pairing(connection);
            var taskListen = await connection.ConnectAndListen(deviceInformation.Address, 
                                                               cancellationToken).ConfigureAwait(false);
            var taskPairing = pairing.StartNewPairing(pin, cancellationToken);

            var completedTask = await Task.WhenAny(taskListen, taskPairing).ConfigureAwait(false);

            if (completedTask == taskPairing)
            {
                return await taskPairing.ConfigureAwait(false);
            }
            else
            {
                await taskListen.ConfigureAwait(false);
                throw new PairingException("Disconnected from accessory while pairing");
            }
        }
    }
}