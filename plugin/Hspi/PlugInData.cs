#nullable enable

namespace Hspi
{
    /// <summary>
    /// Class to store static data
    /// </summary>
    internal static class PlugInData
    {
        /// <summary>
        /// The plugin Id
        /// </summary>
        public const string PlugInId = @"HomeKitController";

        /// <summary>
        /// The plugin name
        /// </summary>
        public const string PlugInName = @"HomeKit Controller";

        public static readonly string DevicePlugInDataNamedKey = PlugInId.ToLowerInvariant() + ".plugindata";

        public static readonly string DevicePlugInDataTypeKey = PlugInId.ToLowerInvariant() + ".plugindatatype";
    }
}