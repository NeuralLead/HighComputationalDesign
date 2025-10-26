using Newtonsoft.Json;
using System;

namespace HighComputationalDesign.Utils
{
    public class GlobalJsonConverter
    {
        public static JsonSerializerSettings settings = new JsonSerializerSettings
        {
            //Converters = { new GlobalJsonConverter() },
            Formatting = Formatting.Indented,
            ContractResolver = new JsonSaveContractResolver()
        };

        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, settings);
        }

        public static T? Deserialize<T>(string jte)
        {
            return JsonConvert.DeserializeObject<T>(jte, settings);
        }

        internal static object? Deserialize(string obj, Type type)
        {
            return JsonConvert.DeserializeObject(obj, type, settings);
        }
    }
}
