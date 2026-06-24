using Sven.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Vocabulaire de commandes piloté par l'ontologie SC4VE, bilingue (UserData.Locale) :
    /// - triggers (sc4ve:trigger, @fr/@en) pour la détection rule-based et la grammaire Vosk ;
    /// - descriptions LLM (rdfs:comment localisé) pour le prompt.
    /// Repli sur les attributs C# ([RuleBasedTriggers] / [CommandDescription]) pour toute
    /// commande pas encore présente dans l'ontologie → migration progressive sans régression.
    /// </summary>
    public static class CommandVocabulary
    {
        private static List<(string[] Triggers, string CommandType)> _triggerMappings;
        private static string _commandsDescription;
        private static string _cachedLocale;

        /// <summary>Mappings (triggers, type) pour la locale courante (ontologie + repli attributs).</summary>
        public static List<(string[] Triggers, string CommandType)> TriggerMappings
            => _triggerMappings ?? RuleBasedTriggersAttribute.GetAllMappings();

        /// <summary>Liste des commandes pour le prompt LLM (ontologie + repli attributs).</summary>
        public static string CommandsDescription
            => _commandsDescription ?? CommandDescriptionAttribute.GetAvailableCommandsString();

        public static async Task InitializeAsync()
        {
            string locale = UserData.Locale;
            if (_triggerMappings != null && _cachedLocale == locale) return;

            Graph graph = new();
            Dictionary<string, string> ontologies = await SvenSettings.GetOntologiesAsync();
            TurtleParser parser = new();
            foreach (string ttl in ontologies.Values) parser.Load(graph, ttl);

            _triggerMappings     = BuildTriggerMappings(graph, locale);
            _commandsDescription = BuildCommandsDescription(graph, locale);
            _cachedLocale        = locale;

            Debug.Log($"[CommandVocab] {_triggerMappings.Count} mapping(s) de triggers, locale '{locale}'.");
        }

        private static List<(string[], string)> BuildTriggerMappings(Graph graph, string locale)
        {
            Dictionary<string, List<string>> onto = QueryByCommand(graph, "sc4ve:trigger", locale);

            // Base = attributs C# (repli) ; remplacés par l'ontologie quand elle a des triggers.
            var mappings = new List<(string[], string)>();
            foreach (var (triggers, commandType) in RuleBasedTriggersAttribute.GetAllMappings())
            {
                if (onto.TryGetValue(commandType, out var ontoTriggers) && ontoTriggers.Count > 0)
                    mappings.Add((ontoTriggers.ToArray(), commandType));
                else
                    mappings.Add((triggers, commandType));
            }
            return mappings;
        }

        private static string BuildCommandsDescription(Graph graph, string locale)
        {
            // Description LLM = rdfs:comment localisé (repli attribut C#) + paramètres requis
            // déduits des restrictions OWL (source unique, pas de duplication).
            Dictionary<string, List<string>> onto = QueryByCommand(graph, "rdfs:comment", locale);
            Dictionary<string, List<string>> required = QueryRequiredParams(graph);
            Dictionary<string, string> attr = CommandDescriptionAttribute.GetAllCommandDescriptions();

            var lines = new List<string>();
            foreach (string cmd in onto.Keys.Concat(attr.Keys).Distinct().OrderBy(x => x))
            {
                string desc = onto.TryGetValue(cmd, out var c) && c.Count > 0 ? c[0]
                            : attr.TryGetValue(cmd, out var a) ? a : null;
                if (desc == null) continue;

                // Ajoute les paramètres (depuis les restrictions) si la description ne les mentionne pas déjà.
                if (!desc.Contains("Paramètres") && required.TryGetValue(cmd, out var ps) && ps.Count > 0)
                    desc += " Paramètres: " + string.Join(", ", ps) + ".";

                lines.Add($"- {cmd}: {desc}");
            }
            return string.Join("\n", lines);
        }

        private static Dictionary<string, List<string>> QueryRequiredParams(Graph graph)
        {
            var result = new Dictionary<string, List<string>>();
            const string query = @"
PREFIX owl: <http://www.w3.org/2002/07/owl#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX sc4ve: <https://sc4ve.lisn.upsaclay.fr/ontology#>
SELECT ?cmd ?param WHERE {
    ?cmd rdfs:subClassOf sc4ve:Command .
    ?cmd rdfs:subClassOf ?r .
    ?r owl:onProperty sc4ve:hasParameter ; owl:onClass ?param .
}";
            if (graph.ExecuteQuery(query) is SparqlResultSet results)
                foreach (SparqlResult row in results.Cast<SparqlResult>())
                {
                    string cmd = LocalName(row["cmd"].ToString());
                    string param = LocalName(row["param"].ToString());
                    if (!result.TryGetValue(cmd, out var list)) result[cmd] = list = new List<string>();
                    if (!list.Contains(param)) list.Add(param);
                }
            return result;
        }

        /// <summary>Valeurs d'une propriété (localisées) regroupées par commande (nom local).</summary>
        private static Dictionary<string, List<string>> QueryByCommand(Graph graph, string property, string locale)
        {
            var result = new Dictionary<string, List<string>>();
            string query = $@"
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX sc4ve: <https://sc4ve.lisn.upsaclay.fr/ontology#>
SELECT ?cmd ?v WHERE {{
    ?cmd rdfs:subClassOf sc4ve:Command .
    ?cmd {property} ?v .
    FILTER(langMatches(lang(?v), ""{locale}""))
}}";
            if (graph.ExecuteQuery(query) is SparqlResultSet results)
                foreach (SparqlResult row in results.Cast<SparqlResult>())
                {
                    string v = (row["v"] as ILiteralNode)?.Value;
                    if (v == null) continue;
                    string cmd = LocalName(row["cmd"].ToString());
                    if (!result.TryGetValue(cmd, out var list)) result[cmd] = list = new List<string>();
                    list.Add(v);
                }
            return result;
        }

        private static string LocalName(string uri)
        {
            int h = uri.LastIndexOf('#');
            if (h >= 0) return uri.Substring(h + 1);
            int s = uri.LastIndexOf('/');
            return s >= 0 ? uri.Substring(s + 1) : uri;
        }
    }
}
