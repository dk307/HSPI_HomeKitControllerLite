using HomeKit;
using HomeKit.Model;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using Hspi.Utils;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Hspi.DeviceData
{
    internal sealed class HsHomeKitCharacteristicFeatureDevice : HsHomeKitFeatureDevice
    {
        public HsHomeKitCharacteristicFeatureDevice(IHsController controller,
                                                    int refId,
                                                    CharacteristicFormat characteristicFormat,
                                                    int? decimalPlacesForValue)
            : base(controller, refId)
        {
            Format = characteristicFormat;
            this.decimalPlacesForValue = decimalPlacesForValue ?? 3;
            var typeData = GetTypeData();
            Debug.Assert(typeData.Type == FeatureType.Characteristics);
            this.Iid = typeData.Iid ?? throw new InvalidOperationException("Invalid PlugExtraData");
            this.cToFNeeded = GetCToFNeeded();
        }

        public CharacteristicFormat Format { get; }

        public ulong Iid { get; }

        public static double C2FConvert(double doubleValue, int decimalPlaces)
        {
            doubleValue = ((doubleValue * 9) / 5) + 32;
            return Math.Round(doubleValue, decimalPlaces);
        }

        public static double F2CConvert(double doubleValue, int decimalPlaces)
        {
            doubleValue = (doubleValue - 32) * 5 / 9;
            return Math.Round(doubleValue, decimalPlaces);
        }

        public object? GetValuetoSend(double value)
        {
            return Format switch
            {
                CharacteristicFormat.Bool => value!= 0 ? 1 : 0,// use 1/0 instead of true/false
                CharacteristicFormat.UnsignedInt8 => TranslateValue<byte>(value),
                CharacteristicFormat.UnsignedInt16 => TranslateValue<UInt16>(value),
                CharacteristicFormat.UnsignedInt32 => TranslateValue<UInt32>(value),
                CharacteristicFormat.UnsignedInt64 => TranslateValue<UInt64>(value),
                CharacteristicFormat.Integer => TranslateValue<int>(value),
                CharacteristicFormat.Float => TranslateValue<double>(value),
                CharacteristicFormat.String => throw new NotImplementedException(),
                CharacteristicFormat.Tlv8 or CharacteristicFormat.DataBlob => throw new NotImplementedException(),
                _ => throw new NotImplementedException(),
            };
        }

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
                case CharacteristicFormat.UnsignedInt64:
                case CharacteristicFormat.Integer:
                case CharacteristicFormat.Float:
                    UpdateDoubleValue<double>(value);
                    break;

                case CharacteristicFormat.String:
                    UpdateDeviceValue(value?.ToString());
                    break;

                case CharacteristicFormat.Tlv8:
                case CharacteristicFormat.DataBlob:
                default:
                    break;
            }
        }

        private bool GetCToFNeeded()
        {
            var plugInExtra = HS.GetPropertyByRef(RefId, EProperty.PlugExtraData) as PlugExtraData;
            return plugInExtra?.ContainsNamed(CToFNeededPlugExtraTag) ?? false;
        }

 
        private object? TranslateValue<T>(double value)
        {
            if (cToFNeeded)
            {
                value = F2CConvert(value, this.decimalPlacesForValue);
            }

            var convertedValue = Convert.ChangeType(value, typeof(T));
            return convertedValue;
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
                    doubleValue = C2FConvert(doubleValue, this.decimalPlacesForValue);
                }

                UpdateDeviceValue(doubleValue);

            }
            catch (Exception ex) when (!ex.IsCancelException())
            {
                Log.Warning("Invalid value {value} received for the {name}", value, NameForLog);
                UpdateDeviceValue(null);
            }
        }

        private readonly bool cToFNeeded;
        private readonly int decimalPlacesForValue;
    }
}