using HomeKit.Model;
using HomeKit.Utils;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;

#nullable enable

namespace Hspi.DeviceData
{
    internal sealed class HsHomeKitRootDevice : HsHomeKitDevice
    {
        public HsHomeKitRootDevice(IHsController controller,
                                   int refId,
                                   HsHomeKitConnectedFeatureDevice connectedFeature,
                                   IEnumerable<HsHomeKitCharacteristicFeatureDevice> characteristicFeatures)
            : base(controller, refId)
        {
            CharacteristicFeatures = characteristicFeatures.ToImmutableDictionary(x => x.RefId);
            ConnectedFeature = connectedFeature;
        }

        public ImmutableDictionary<int, HsHomeKitCharacteristicFeatureDevice> CharacteristicFeatures { get; }
        public HsHomeKitConnectedFeatureDevice ConnectedFeature { get; }

        public static ulong GetAid(IHsController hsController, int refId)
        {
            return GetPlugExtraData<ulong>(hsController, refId, AidPlugExtraTag);
        }

        public static ImmutableArray<ulong> GetEnabledCharacteristic(PlugExtraData plugExtraData)
        {
            return GetPlugExtraData<ImmutableArray<ulong>>(plugExtraData,
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

        public ulong GetAid()
        {
            return GetPlugExtraData<ulong>(AidPlugExtraTag);
        }

        public IPEndPoint GetFallBackAddress()
        {
            return GetPlugExtraData<IPEndPoint>(FallbackAddressPlugExtraTag, new IPEndPointJsonConverter());
        }

        public void SetConnectedState(bool connected) => ConnectedFeature.SetConnectedState(connected);

        public void SetFallBackAddress(IPEndPoint endPoint)
        {
            if (HS.GetPropertyByRef(RefId, EProperty.PlugExtraData) is not PlugExtraData plugInExtra)
            {
                plugInExtra = new PlugExtraData();
            }

            plugInExtra[FallbackAddressPlugExtraTag] = JsonConvert.SerializeObject(endPoint, new IPEndPointJsonConverter());
            HS.UpdatePropertyByRef(RefId, EProperty.PlugExtraData, plugInExtra);
        }

        public PairingDeviceInfo GetPairingInfo()
        {
            return GetPlugExtraData<PairingDeviceInfo>(PairInfoPlugExtraTag);
        }
    }
}