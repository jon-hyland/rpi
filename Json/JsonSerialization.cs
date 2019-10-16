using Newtonsoft.Json;

namespace Rpi.Json
{
    /// <summary>
    /// Simple class to serialize objects to string (JSON format), and back again.
    /// </summary>
    public static class JsonSerialization
    {
        /// <summary>
        /// Serialize object to binary.  
        /// Optionally include type name in result, for direct object deserialization.
        /// </summary>
        public static string Serialize(object value, bool typeNameHandling = false)
        {
            if (value == null)
                return null;
            string json = JsonConvert.SerializeObject(value, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = typeNameHandling ? TypeNameHandling.All : TypeNameHandling.None
            });
            return json;
        }

        /// <summary>
        /// Serialize object to binary.  
        /// Optionally use type name in result, for direct object deserialization.
        /// </summary>
        public static object Deserialize(string json, bool typeNameHandling = false)
        {
            object value = JsonConvert.DeserializeObject(json, new JsonSerializerSettings
            {
                TypeNameHandling = typeNameHandling ? TypeNameHandling.All : TypeNameHandling.None
            });
            return value;
        }
    }
}
