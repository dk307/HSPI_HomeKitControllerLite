using HomeKit.Model;
using HomeKit.Utils;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Newtonsoft.Json;
using System;
using System.Collections.Immutable;
using System.Net;

#nullable enable

namespace Hspi.DeviceData
{
    internal class HsHomeKitBaseRootDevice : HsHomeKitDevice
    {
        public HsHomeKitBaseRootDevice(IHsController controller, int refId)
            : base(controller, refId)
        {
            this.Aid = GetAid();
        }

        public ulong Aid { get; }

        public Accessory CachedAccessoryInfo => GetPlugExtraData<Accessory>(CachedAccessoryInfoTag);

        public ImmutableSortedSet<ulong> EnabledCharacteristic => GetPlugExtraData<ImmutableSortedSet<ulong>>(EnabledCharacteristicPlugExtraTag);

        public IPEndPoint FallBackAddress => GetPlugExtraData<IPEndPoint>(FallbackAddressPlugExtraTag, new IPEndPointJsonConverter());

        public PairingDeviceInfo PairingInfo => GetPlugExtraData<PairingDeviceInfo>(PairInfoPlugExtraTag);

        public static ImmutableSortedSet<ulong> GetEnabledCharacteristic(PlugExtraData plugExtraData)
        {
            return GetPlugExtraData<ImmutableSortedSet<ulong>>(plugExtraData,
                                                               EnabledCharacteristicPlugExtraTag);
        }

        public static IPEndPoint GetFallBackAddress(IHsController hsController, int refId)
        {
            return GetPlugExtraData<IPEndPoint>(hsController,
                                                refId,
                                                FallbackAddressPlugExtraTag,
                                                new IPEndPointJsonConverter());
        }

        public static PairingDeviceInfo GetPairingInfo(IHsController hsController, int refId)
        {
            return GetPlugExtraData<PairingDeviceInfo>(hsController, refId, PairInfoPlugExtraTag);
        }
        public void SetKeepAliveForConnection(bool enableKeepAliveForConnection)
        {
            var data = PairingInfo with { EnableKeepAliveForConnection = enableKeepAliveForConnection };
            UpdatePlugExtraData(PairInfoPlugExtraTag, JsonConvert.SerializeObject(data));
        }

        public void SetPollingInterval(TimeSpan? interval)
        {
            var data = PairingInfo with { PollingTimeSpan = interval };
            UpdatePlugExtraData(PairInfoPlugExtraTag, JsonConvert.SerializeObject(data));
        }

        private ulong GetAid()
        {
            return GetPlugExtraData<ulong>(AidPlugExtraTag);
        }
    }
}