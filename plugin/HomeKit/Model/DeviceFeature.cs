using System;

#nullable enable


namespace HomeKit.Model
{
    [Flags]
    public enum DeviceFeature
    {
        None = 0,
        SupportsAppleAuthenticationCoprocessor = 1,
        SupportsSoftwareAuthentication = 2,
    }
}