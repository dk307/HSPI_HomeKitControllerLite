using HomeKit.Model;
using HomeKit.Utils;
using HomeSeer.PluginSdk;
using Newtonsoft.Json;
using Serilog;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;

#nullable enable

namespace Hspi.DeviceData
{
    internal sealed class HsHomeKitRootDevice : HsHomeKitBaseRootDevice
    {
        public HsHomeKitRootDevice(IHsController controller,
                                   int refId,
                                   HsHomeKitConnectedFeatureDevice connectedFeature,
                                   IEnumerable<HsHomeKitCharacteristicFeatureDevice> characteristicFeatures)
            : base(controller, refId)
        {
            CharacteristicFeatures = characteristicFeatures.ToImmutableDictionary(x => x.Iid);
            ConnectedFeature = connectedFeature;
        }

        // Iid to device dict
        public ImmutableDictionary<ulong, HsHomeKitCharacteristicFeatureDevice> CharacteristicFeatures { get; }

        public HsHomeKitConnectedFeatureDevice ConnectedFeature { get; }

        public static ulong GetAid(IHsController hsController, int refId)
        {
            return GetPlugExtraData<ulong>(hsController, refId, AidPlugExtraTag);
        }

        public AidIidPair? GetCharacteristicAidIidValue(int refId)
        {
            if (CharacteristicFeatures.Values.FirstOrDefault(x => x.RefId == refId)
                        is HsHomeKitCharacteristicFeatureDevice hsHomeKitCharacteristicFeatureDevice)
            {
                return new AidIidPair(Aid, hsHomeKitCharacteristicFeatureDevice.Iid);
            }
            return null;
        }

        public (bool, AidIidValue?) GetValueToSend(int refId, double value)
        {
            if (refId == this.RefId)
            {
                Log.Warning("Unknown command {command} for {RefId} ", value, RefId);
                return (true, null);
            }
            else if (CharacteristicFeatures.Values.FirstOrDefault(x => x.RefId == refId)
                        is HsHomeKitCharacteristicFeatureDevice hsHomeKitCharacteristicFeatureDevice)
            {
                var valueToSend = hsHomeKitCharacteristicFeatureDevice.GetValuetoSend(value);
                return (true, new AidIidValue(Aid, hsHomeKitCharacteristicFeatureDevice.Iid, valueToSend));
            }
            else if (ConnectedFeature.RefId == refId)
            {
                Log.Warning("Unknown command {command} for Connected Device {RefId} ", value, RefId);
                return (true, null);
            }

            return (false, null);
        }

        public void SetConnectedState(bool connected) => ConnectedFeature.SetConnectedState(connected);

        public void SetTransientAccesssoryValues(IPEndPoint address, Accessory accessory)
        {
            UpdatePlugExtraData(new KeyValuePair<string, string>(FallbackAddressPlugExtraTag,
                                                                 JsonConvert.SerializeObject(address, new IPEndPointJsonConverter())),
                                new KeyValuePair<string, string>(CachedAccessoryInfoTag, JsonConvert.SerializeObject(accessory)));
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
    }
}