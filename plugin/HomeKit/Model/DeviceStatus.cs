using System;

namespace HomeKit.Model
{
    [Flags]
    public enum DeviceStatus
    {
        None = 0,
        NotPaired = 1,
        NotConfiguredToJoinWifi = 2,
        ProblemDetected = 4,
    }
}