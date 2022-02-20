using HomeKit.Model;
using HomeKit.Utils;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Hspi.Exceptions;
using Newtonsoft.Json;
using System.Net;
using static System.FormattableString;

#nullable enable

namespace Hspi.DeviceData
{
    internal sealed class HsHomeKitDevice
    {
        public HsHomeKitDevice(IHsController controller, int refId)
        {
            HS = controller;
            RefId = refId;
        }

        public enum DeviceType
        {
            Root = 0,
            OnlineStatus = 1,
            Characteristics = 2
        }

        public int RefId { get; init; }

        private IHsController HS { get; init; }

        public ulong GetAid()
        {
            return GetPlugExtraData<ulong>(AidPlugExtraTag);
        }

        public bool GetCToFNeeded()
        {
            var plugInExtra = HS.GetPropertyByRef(RefId, EProperty.PlugExtraData) as PlugExtraData;
            var stringData = plugInExtra?[CToFNeededPlugExtraTag];
            return (stringData != null);
        }

        public IPEndPoint GetFallBackAddress()
        {
            return GetPlugExtraData<IPEndPoint>(FallbackAddressPlugExtraTag, new IPEndPointJsonConverter());
        }
        public PairingDeviceInfo GetPairingInfo()
        {
            return GetPlugExtraData<PairingDeviceInfo>(PairInfoPlugExtraTag);
        }

        public T GetPlugExtraData<T>(string tag, params JsonConverter[] converters)
        {
            var plugInExtra = HS.GetPropertyByRef(RefId, EProperty.PlugExtraData) as PlugExtraData;
            var stringData = plugInExtra?[tag];
            if (stringData == null)
            {
                throw new HsDeviceInvalidException(Invariant($"{tag} type not found for {RefId}"));
            }

            var typeData = JsonConvert.DeserializeObject<T>(stringData, converters);
            if (typeData == null)
            {
                throw new HsDeviceInvalidException(Invariant($"{tag} not a valid Json for {RefId}"));
            }

            return typeData;
        }

        public DeviceTypeData GetTypeData()
        {
            var typeData = GetPlugExtraData<DeviceTypeData>(DeviceTypePlugExtraTag);

            if ((typeData.Type == DeviceType.Characteristics) && (typeData.Iid == null))
            {
                throw new HsDeviceInvalidException(Invariant($"Device Type data not valid for {RefId}"));
            }

            return typeData;
        }

        //Extra data Tags
        public const string AidPlugExtraTag = "Accessory.Aid";
        public const string CToFNeededPlugExtraTag = "C2F.needed";
        public const string DeviceTypePlugExtraTag = "Device.Type";
        public const string FallbackAddressPlugExtraTag = "Fallback.Address";
        public const string PairInfoPlugExtraTag = "Pairing.Info";
    }
}