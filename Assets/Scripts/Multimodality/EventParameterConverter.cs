#if UNITY_EDITOR
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace Sven.Command
{
    public class EventParameterConverter : JsonConverter<EventParameter>
    {
        public override EventParameter ReadJson(JsonReader reader, Type objectType, EventParameter existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            // Crée une nouvelle instance en utilisant ScriptableObject.CreateInstance
            var instance = ScriptableObject.CreateInstance<EventParameter>();

            // Peuple l'instance avec les données JSON
            JObject jObject = JObject.Load(reader);
            serializer.Populate(jObject.CreateReader(), instance);

            return instance;
        }

        public override void WriteJson(JsonWriter writer, EventParameter value, JsonSerializer serializer)
        {
            // La sérialisation par défaut est suffisante pour l'écriture.
            // Nous devons juste nous assurer de ne pas entrer dans une boucle infinie.
            var tempSerializer = new JsonSerializer
            {
                TypeNameHandling = serializer.TypeNameHandling,
                Formatting = serializer.Formatting
            };
            foreach (var converter in serializer.Converters)
            {
                if (converter != this)
                {
                    tempSerializer.Converters.Add(converter);
                }
            }
            tempSerializer.Serialize(writer, value);
        }
    }
}
#endif