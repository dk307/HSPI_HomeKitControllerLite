using System;

#nullable enable

namespace HomeKit.Utils
{
    [System.AttributeUsage(System.AttributeTargets.All)]
    public class UnitAttribute : Attribute
    {
        public UnitAttribute(string unit)
        {
            Unit = unit;
        }

        public string Unit { get; }
    }
}