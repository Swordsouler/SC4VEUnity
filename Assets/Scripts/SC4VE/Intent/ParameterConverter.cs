using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;

namespace Sc4ve.Multimodality.Parameter
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
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            // Create a temporary serializer that reuses configuration but prevents this converter
            // from being used via attributes. When a type has [JsonConverter(...)] applied,
            // that converter is used even if not present in serializer.Converters; to avoid
            // recursion we provide a ContractResolver wrapper that clears the converter for
            // Parameter-derived types and we also ignore reference loops.
            var innerResolver = serializer.ContractResolver ?? new DefaultContractResolver();
            var tempSerializer = new JsonSerializer
            {
                Culture = serializer.Culture,
                Formatting = serializer.Formatting,
                DateFormatHandling = serializer.DateFormatHandling,
                DateFormatString = serializer.DateFormatString,
                DateTimeZoneHandling = serializer.DateTimeZoneHandling,
                NullValueHandling = serializer.NullValueHandling,
                DefaultValueHandling = serializer.DefaultValueHandling,
                ContractResolver = new ParameterIgnoringContractResolver(innerResolver),
                FloatFormatHandling = serializer.FloatFormatHandling,
                StringEscapeHandling = serializer.StringEscapeHandling,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            // Copy other converters except this converter's runtime type to be safe.
            foreach (var conv in serializer.Converters)
            {
                if (conv != null && conv.GetType() != typeof(ParameterConverter))
                    tempSerializer.Converters.Add(conv);
            }

            var jo = JObject.FromObject(value, tempSerializer);

            // Évite l'ArgumentException si la propriété "type" existe déjà :
            var existingTypeProp = jo.Property("type");
            if (existingTypeProp != null)
            {
                existingTypeProp.Value = value.GetType().Name;
            }
            else
            {
                jo.AddFirst(new JProperty("type", value.GetType().Name));
            }

            jo.WriteTo(writer);
        }

        // ContractResolver wrapper that clears any converter for Parameter-derived types
        // to prevent attribute-based re-entry into this converter during serialization.
        private class ParameterIgnoringContractResolver : IContractResolver
        {
            private readonly IContractResolver _inner;

            public ParameterIgnoringContractResolver(IContractResolver inner)
            {
                _inner = inner ?? new DefaultContractResolver();
            }

            public JsonContract ResolveContract(System.Type type)
            {
                var contract = _inner.ResolveContract(type);

                if (typeof(Parameter).IsAssignableFrom(type) && contract != null)
                {
                    // Clear converters that could cause recursive usage.
                    contract.Converter = null;

                    if (contract is JsonContainerContract containerContract)
                    {
                        containerContract.ItemConverter = null;
                    }
                }

                return contract;
            }
        }
    }
}