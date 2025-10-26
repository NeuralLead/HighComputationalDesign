using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HighComputationalDesign.Utils
{
    [AttributeUsage(AttributeTargets.Property)]
    public class JsonSaveAttribute : Attribute
    {
    }

    public class JsonSaveContractResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            // Prendo tutte le proprietà standard
            var properties = base.CreateProperties(type, memberSerialization);

            // Filtro: prendo solo quelle che hanno [JsonSave]
            return properties
                .Where(p =>
                {
                    var propInfo = type.GetProperty(p.UnderlyingName ?? "");
                    return propInfo?.GetCustomAttribute<JsonSaveAttribute>() != null;
                })
                .ToList();
        }
    }
}
