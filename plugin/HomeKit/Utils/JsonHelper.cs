using Newtonsoft.Json;

namespace Hspi.HomeKit.Utils
{
    internal static class JsonHelper
    {
        public static T DeserializeObject<T>(string value) where T : class
        {
            return JsonConvert.DeserializeObject<T>(value) ??
                        throw new JsonReaderException("Null returned");
        }
    }
}