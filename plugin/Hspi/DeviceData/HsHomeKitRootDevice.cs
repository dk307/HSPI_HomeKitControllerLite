using HomeKit.Model;
using HomeKit.Utils;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using Newtonsoft.Json;
using Serilog;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
            CharacteristicFeatures = characteristicFeatures.ToImmutableDictionary(x => x.Iid);
            ConnectedFeature = connectedFeature;
            this.Aid = GetAid();
        }

        public ulong Aid { get; }

        // Iid to device dict
        public ImmutableDictionary<ulong, HsHomeKitCharacteristicFeatureDevice> CharacteristicFeatures { get; }

        public HsHomeKitConnectedFeatureDevice ConnectedFeature { get; }

        public static ulong GetAid(IHsController hsController, int refId)
        {
            return GetPlugExtraData<ulong>(hsController, refId, AidPlugExtraTag);
        }

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

        public IPEndPoint GetFallBackAddress()
        {
            return GetPlugExtraData<IPEndPoint>(FallbackAddressPlugExtraTag, new IPEndPointJsonConverter());
        }

        public PairingDeviceInfo GetPairingInfo()
        {
            return GetPlugExtraData<PairingDeviceInfo>(PairInfoPlugExtraTag);
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

        public void SetValue(ulong iid, object? value)
        {
            if (CharacteristicFeatures.TryGetValue(iid, out var feature))
            {
                feature.SetValue(value);
            }
            else
            {
                Log.Debug("Unknown iid {iid} received for {aid} from {name}", iid, Aid, NameForLog);
            }
        }

        private ulong GetAid()
        {
            return GetPlugExtraData<ulong>(AidPlugExtraTag);
        }

        public (bool, AidIidValue?) GetValueToSend(ControlEvent colSend)
        {
            int refId = colSend.TargetRef;

            if (refId == this.RefId)
            {
                Log.Warning("Unknown command {command} for {RefId} ", colSend.ControlValue, RefId);
                return (true, null);
            }
            else if (CharacteristicFeatures.Values.FirstOrDefault(x => x.RefId == refId)
                        is HsHomeKitCharacteristicFeatureDevice hsHomeKitCharacteristicFeatureDevice)
            {
                var valueToSend = hsHomeKitCharacteristicFeatureDevice.GetValuetoSend(colSend);
                return (true, new AidIidValue(Aid, hsHomeKitCharacteristicFeatureDevice.Iid, valueToSend));
            }
            else if (ConnectedFeature.RefId == refId)
            {
                Log.Warning("Unknown command {command} for Connected Device {RefId} ", colSend.ControlValue, RefId);
                return (true, null);
            }

            return (false, null);
        }
    }
}