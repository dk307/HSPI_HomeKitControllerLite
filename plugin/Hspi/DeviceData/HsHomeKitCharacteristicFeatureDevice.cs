using HomeKit.Model;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Diagnostics;

#nullable enable

namespace Hspi.DeviceData
{
    internal sealed class HsHomeKitCharacteristicFeatureDevice : HsHomeKitFeatureDevice
    {
        public HsHomeKitCharacteristicFeatureDevice(IHsController controller,
                                                    int refId,
                                                    CharacteristicFormat characteristicFormat)
            : base(controller, refId)
        {
            Format = characteristicFormat;
            var typeData = GetTypeData();
            Debug.Assert(typeData.Type == FeatureType.Characteristics);
            this.Iid = typeData.Iid ?? throw new InvalidOperationException("Invalid PlugExtraData");
            this.cToFNeeded = GetCToFNeeded();
        }

        public CharacteristicFormat Format { get; }

        public ulong Iid { get; }

        public void SetValue(object? value)
        {
            switch (Format)
            {
                case CharacteristicFormat.Bool:
                    UpdateDoubleValue<bool>(value);
                    break;

                case CharacteristicFormat.UnsignedInt8:
                case CharacteristicFormat.UnsignedInt16:
                case CharacteristicFormat.UnsignedInt32:
                case CharacteristicFormat.Integer:
                case CharacteristicFormat.Float:
                    UpdateDoubleValue<double>(value);
                    break;

                case CharacteristicFormat.String:
                    UpdateDeviceValue(value?.ToString());
                    break;

                case CharacteristicFormat.Tlv8:
                case CharacteristicFormat.DataBlob:

                    throw new NotImplementedException();
                default:
                    break;
            }
        }

        private bool GetCToFNeeded()
        {
            var plugInExtra = HS.GetPropertyByRef(RefId, EProperty.PlugExtraData) as PlugExtraData;
            return plugInExtra?.ContainsNamed(CToFNeededPlugExtraTag) ?? false;
        }
        private void UpdateDoubleValue<T>(object? value)
        {
            try
            {
                if (value == null)
                {
                    UpdateDeviceValue(null);
                    return;
                }
                JValue jValue = new(value);
                var doubleValue = Convert.ToDouble(jValue.Value<T>());

                if (cToFNeeded)
                {
                    doubleValue = ((doubleValue * 9) / 5) + 32;
                }
                UpdateDeviceValue(doubleValue);
                Log.Debug("Updated value {value} for the {name}", value, NameForLog);
            }
            catch (Exception)
            {
                Log.Warning("Invalid value {value} received for the {name}", value, NameForLog);
                UpdateDeviceValue(null);
            }
        }

        private readonly bool cToFNeeded;
    }
}