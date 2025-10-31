using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Sc4ve.Multimodality
{
    internal class ParameterConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(Parameter).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            var typeToken = obj["type"];
            var typeStr = typeToken?.ToString();

            Parameter target = typeStr switch
            {
                "ColorParameter" => new ColorParameter(),
                "ColorizeParameter" => new ColorParameter(), // alias si nécessaire
                "PositionParameter" => new PositionParameter(),
                "SelectionParameter" => new SelectionParameter(),
                _ => new Parameter()
            };

            using (var sr = obj.CreateReader())
            {
                serializer.Populate(sr, target);
            }

            return target;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}