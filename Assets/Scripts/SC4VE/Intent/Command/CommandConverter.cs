using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    internal class CommandConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(Command).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            var typeToken = obj["type"];
            var typeStr = typeToken?.ToString();

            Command target = typeStr switch
            {
                "ColorizeCommand" => new ColorizeCommand(),
                "GrabCommand" => new GrabCommand(),
                "HideCommand" => new HideCommand(),
                "MeasureCommand" => new MeasureCommand(),
                "MoveCommand" => new MoveCommand(),
                "ReleaseCommand" => new ReleaseCommand(),
                "ScaleDownCommand" => new ScaleDownCommand(),
                "ScaleUpCommand" => new ScaleUpCommand(),
                "SelectCommand" => new SelectCommand(),
                "ShowCommand" => new ShowCommand(),
                "UnselectCommand" => new UnselectCommand(),
                _ => new UnknownCommand { Type = typeStr ?? "Unknown" }
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

            // Evite la récursion due au JsonConverter appliqué par attribut sur Command.
            // On crée un serializer temporaire qui réutilise la configuration courante
            // mais remplace le ContractResolver pour neutraliser les converters définis
            // par attributs sur les types dérivés de Command et on évite les boucles.
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
                ContractResolver = new CommandIgnoringContractResolver(innerResolver),
                FloatFormatHandling = serializer.FloatFormatHandling,
                StringEscapeHandling = serializer.StringEscapeHandling,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            // Copie les autres converters sauf celui-ci pour être sûr.
            foreach (var conv in serializer.Converters)
            {
                if (conv != null && conv.GetType() != typeof(CommandConverter))
                    tempSerializer.Converters.Add(conv);
            }

            var jo = JObject.FromObject(value, tempSerializer);

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
    }

    // Résout le contrat en neutralisant les JsonConverter pour les types dérivés de Command.
    internal class CommandIgnoringContractResolver : IContractResolver
    {
        private readonly IContractResolver _inner;

        public CommandIgnoringContractResolver(IContractResolver inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public JsonContract ResolveContract(Type type)
        {
            var contract = _inner.ResolveContract(type);
            if (typeof(Command).IsAssignableFrom(type))
            {
                // Retirer tout converter appliqué par attribut ou par contrat pour éviter récursion
                contract.Converter = null;
            }
            return contract;
        }
    }

    [Serializable]
    public class UnknownCommand : Command
    {
        public override List<SemantizationCore> Execute()
        {
            Debug.LogWarning($"UnknownCommand executed (original type: {Type}). No action performed.");
            return new();
        }
    }
}