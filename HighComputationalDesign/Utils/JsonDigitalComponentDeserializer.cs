using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Reflection;
using HighComputationalDesign.Models;

namespace HighComputationalDesign.Utils
{
    public class JsonDigitalComponentDeserializer : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(DigitalComponent).IsAssignableFrom(objectType);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            /*var a = GlobalJsonConverter.Serialize(value);
            if (a != null)
                writer.WriteRawValue(a);*/


            writer.WriteStartObject();

            var type = value.GetType();
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                if (prop.GetCustomAttribute<JsonSaveAttribute>() != null)
                {
                    var propValue = prop.GetValue(value);
                    writer.WritePropertyName(prop.Name);
                    serializer.Serialize(writer, propValue);
                }
            }

            writer.WriteEndObject();
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var tok = JToken.ReadFrom(reader);
            if (!tok.HasValues)
                return null;

            //JObject jox = JObject.Load(reader);

            string? tipo = tok["Typized"]?.ToString();
            if (tipo is null)
            {
                //throw new Exception("Dataset type not specified");
                return null;
            }

            // Cerca il tipo in tutti gli assembly caricati
            Type? foundType = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(a => a.GetType(tipo, false)) // false per evitare errori se non trovato
                .FirstOrDefault(t => t != null);

            if (foundType is null)
                throw new Exception("Neuron model cannot be found. Install the right plugin or restart NeuralLead Maker and check if all plugins are correctly loaded");

            /*IDigitalComponent? obj = GlobalJsonConverter.Deserialize(tok.ToString(), foundType) as IDigitalComponent;
            //INeuronModel? obj = Activator.CreateInstance(foundType) as INeuronModel;            
            if (obj is null)
                throw new Exception($"Cannot deserialize Dataset type {tipo}");
            serializer.Populate(tok.CreateReader(), obj);*/

            // crea istanza "vuota"
            var obj = Activator.CreateInstance(foundType) as DigitalComponent;
            if (obj is null)
                throw new Exception($"Non riesco a istanziare {tipo}");

            // Popola con i dati del token
            using (var sr = tok.CreateReader())
            {
                serializer.Populate(sr, obj);
            }

            return obj;
        }
    }
}
