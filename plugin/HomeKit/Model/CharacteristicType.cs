#nullable enable

using System.Collections.Generic;

namespace HomeKit.Model
{
    public sealed record CharacteristicType : BaseGuidType
    {
        public CharacteristicType(string value) : base(value)
        {
            lock (namesLock)
            {
                names.TryGetValue(this, out var DisplayName);
            }
        }

        public CharacteristicType(string value, string preDefinedName) : base(value)
        {
            lock (namesLock)
            {
                names.Add(this, preDefinedName);
            }
        }

        private static readonly Dictionary<CharacteristicType, string> names = new();
        private static readonly object namesLock = new();

        public bool Equals(CharacteristicType other) => this.Id == other?.Id;

        public override int GetHashCode() => Id.GetHashCode();

        public string? DisplayName { get; init; }

        public override string ToString()
        {
            return this.DisplayName ?? Id.ToString();
        }

        public static readonly CharacteristicType AccessoryIdentifier = new("57", nameof(AccessoryIdentifier));
        public static readonly CharacteristicType AccessoryProperties = new("A6", nameof(AccessoryProperties));
        public static readonly CharacteristicType ButtonEvent = new("126", nameof(ButtonEvent));
        public static readonly CharacteristicType Category = new("A3", nameof(Category));
        public static readonly CharacteristicType Name = new("23", nameof(Name));
        public static readonly CharacteristicType Version = new("37", nameof(Version));
        public static readonly CharacteristicType Identify = new("14", nameof(Identify));
        public static readonly CharacteristicType Manufacturer = new("20", nameof(Manufacturer));
        public static readonly CharacteristicType Model = new("21", nameof(Model));
        public static readonly CharacteristicType SerialNumber = new("30", nameof(SerialNumber));
        public static readonly CharacteristicType FirmwareRevision = new("52", nameof(FirmwareRevision));
        public static readonly CharacteristicType HardwareRevision = new("53", nameof(HardwareRevision));
        public static readonly CharacteristicType ProductData = new("220", nameof(ProductData));

        public static readonly CharacteristicType TemperatureCurrent = new("11", nameof(TemperatureCurrent));
        public static readonly CharacteristicType TemperatureUnits = new("36", nameof(TemperatureUnits));
        public static readonly CharacteristicType TemperatureCoolingThreshold = new("0D", nameof(TemperatureCoolingThreshold));
        public static readonly CharacteristicType TemperatureHeatingThreshold = new("12", nameof(TemperatureHeatingThreshold));
        public static readonly CharacteristicType TemperatureTarget = new("35", nameof(TemperatureTarget));

        public static readonly CharacteristicType RelativeHumidityCurrent = new("10", nameof(RelativeHumidityCurrent));
        public static readonly CharacteristicType RelativeHumidityTarget = new("34", nameof(RelativeHumidityTarget));
        public static readonly CharacteristicType HeatingCoolingCurrent = new("F", nameof(HeatingCoolingCurrent));
        public static readonly CharacteristicType HeatingCoolingTarget = new("33", nameof(HeatingCoolingTarget));
        public static readonly CharacteristicType FanStateCurrent = new("AF", nameof(FanStateCurrent));
        public static readonly CharacteristicType FanStateTarget = new("BF", nameof(FanStateTarget));

        public static readonly CharacteristicType MotionDetected = new("22", nameof(MotionDetected));
        public static readonly CharacteristicType OccupancyDetected = new("71", nameof(OccupancyDetected));
        public static readonly CharacteristicType SupportedAudioConfiguration = new("115", nameof(SupportedAudioConfiguration));
        public static readonly CharacteristicType SelectedAudioStreemConfiguration = new("128", nameof(SupportedAudioConfiguration));
        public static readonly CharacteristicType SiriInputType = new("132", nameof(SiriInputType));
        public static readonly CharacteristicType SiriEnable = new("255", nameof(SiriEnable));
        public static readonly CharacteristicType SiriListening = new("256", nameof(SiriListening));

        // Ecobee
        // r/o, uint8 - current mode - home(0)/sleep(1)/away(2)/temp(3)
        public static readonly CharacteristicType VendorEcobeeCurrentMode =
                        new("B7DDB9A3-54BB-4572-91D2-F1F5B0510F8C", nameof(VendorEcobeeCurrentMode));

        // r/w, float - home heat temperature between 7.2 and 26.1
        public static readonly CharacteristicType VendorEcobeeHomeTargetHeat =
                        new("E4489BBC-5227-4569-93E5-B345E3E5508F", nameof(VendorEcobeeHomeTargetHeat));

        // r/w, float - home cool temperature between 18.3 and 33.3
        public static readonly CharacteristicType VendorEcobeeHomeTargetCool =
                        new("7D381BAA-20F9-40E5-9BE9-AEB92D4BECEF", nameof(VendorEcobeeHomeTargetCool));

        // r/w, float - sleep heat temperature between 7.2 and 26.1
        public static readonly CharacteristicType VendorEcobeeSleepTargetHeat =
                        new("73AAB542-892A-4439-879A-D2A883724B69", nameof(VendorEcobeeSleepTargetHeat));

        // r/w, float - sleep cool temperature between 18.3 and 33.3
        public static readonly CharacteristicType VendorEcobeeSleepTargetCool =
                        new("5DA985F0-898A-4850-B987-B76C6C78D670", nameof(VendorEcobeeSleepTargetCool));

        // r/w, float - away heat temp between 7.2 and 26.1
        public static readonly CharacteristicType VendorEcobeeAwayTargetHeat =
                        new("05B97374-6DC0-439B-A0FA-CA33F612D425", nameof(VendorEcobeeAwayTargetHeat));

        // r/w, float - away cool temp between 18.3 and 33.3
        public static readonly CharacteristicType VendorEcobeeAwayTargetCool =
                        new("A251F6E7-AC46-4190-9C5D-3D06277BDF9F", nameof(VendorEcobeeAwayTargetCool));

        // w/o, uint8 - set hold schedule mode - home(0)/sleep(1)/away(2)
        public static readonly CharacteristicType VendorEcobeeSetHoldSchedule =
                        new("1B300BC2-CFFC-47FF-89F9-BD6CCF5F2853", nameof(VendorEcobeeSetHoldSchedule));

        // r/w, string - 2014-01-03T00:00:00-07:00T
        public static readonly CharacteristicType VendorEcobeeTimestamp =
                        new("1621F556-1367-443C-AF19-82AF018E99DE", nameof(VendorEcobeeTimestamp));

        // w/o, bool - true to clear hold mode, false does nothing
        public static readonly CharacteristicType VendorEcobeeClearHold =
                        new("FA128DE6-9D7D-49A4-B6D8-4E4E234DEE38", nameof(VendorEcobeeClearHold));

        // r/w, 100 for on, 0 for off/auto
        // https://support.ecobee.com/s/articles/Multi-Speed-Fan-installations
        public static readonly CharacteristicType VendorEcobeeFanWriteSpeed =
                        new("C35DA3C0-E004-40E3-B153-46655CDD9214", nameof(VendorEcobeeFanWriteSpeed));

        // r/o, Mirrors status of above
        public static readonly CharacteristicType VendorEcobeeFanReadSpeed =
                        new("48F62AEC-4171-4B4A-8F0E-1EEB6708B3FB", nameof(VendorEcobeeFanReadSpeed));
    }
}