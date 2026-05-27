using System;
using System.Collections.Generic;
using System.Linq;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Déclare les mots-déclencheurs RuleBased d'une commande.
    /// Découvert automatiquement par réflexion — aucun enregistrement dans le recognizer requis.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class RuleBasedTriggersAttribute : Attribute
    {
        public string[] Triggers { get; }

        public RuleBasedTriggersAttribute(params string[] triggers)
        {
            Triggers = triggers ?? Array.Empty<string>();
        }

        private static List<(string[] Triggers, string CommandType)> _cache;

        /// <summary>
        /// Retourne tous les couples (déclencheurs, nom de commande) découverts par réflexion.
        /// Résultat mis en cache après le premier appel.
        /// </summary>
        public static List<(string[] Triggers, string CommandType)> GetAllMappings()
        {
            if (_cache != null) return _cache;

            _cache = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => t.IsClass && !t.IsAbstract && typeof(Command).IsAssignableFrom(t))
                .Select(t => (
                    Attr: (RuleBasedTriggersAttribute)Attribute.GetCustomAttribute(
                              t, typeof(RuleBasedTriggersAttribute)),
                    Name: t.Name
                ))
                .Where(x => x.Attr != null)
                .Select(x => (x.Attr.Triggers, x.Name))
                .ToList();

            return _cache;
        }
    }
}
