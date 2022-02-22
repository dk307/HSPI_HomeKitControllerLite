using HomeKit.Model;
using HomeSeer.PluginSdk;
using Serilog;
using System;
using System.ComponentModel;
using System.Globalization;

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
        }

        public CharacteristicFormat Format { get; }

        public void SetValue(object value)
        {
            switch (Format)
            {
                case CharacteristicFormat.Bool:
                    {
                        TypeConverter typeConverter = TypeDescriptor.GetConverter(typeof(bool));
                        if (typeConverter.IsValid(value))
                        {
                            var boolValue = (bool)typeConverter.ConvertFrom(null, CultureInfo.InvariantCulture, value);
                            UpdateDeviceValue(boolValue ? 1 : 0);
                        }
                        else
                        {
                            Log.Warning("Invalid value {value} received for the {name}", value, NameForLog);
                            UpdateDeviceValue(null);
                        }
                    }
                    break;

                case CharacteristicFormat.UnsignedInt8:
                case CharacteristicFormat.UnsignedInt16:
                case CharacteristicFormat.UnsignedInt32:
                case CharacteristicFormat.Integer:
                case CharacteristicFormat.Float:
                    {
                        TypeConverter typeConverter = TypeDescriptor.GetConverter(typeof(double));
                        if (typeConverter.IsValid(value))
                        {
                            var doubleValue = (double)typeConverter.ConvertFrom(null, CultureInfo.InvariantCulture, value);
                            UpdateDeviceValue(doubleValue);
                        }
                        else
                        {
                            Log.Warning("Invalid value {value} received for the {name}", value, NameForLog);
                            UpdateDeviceValue(null);
                        }
                    }
                    break;

                case CharacteristicFormat.String:
                    break;

                case CharacteristicFormat.Tlv8:
                case CharacteristicFormat.DataBlob:

                    throw new NotImplementedException();
                default:
                    break;
            }
        }
    }
}