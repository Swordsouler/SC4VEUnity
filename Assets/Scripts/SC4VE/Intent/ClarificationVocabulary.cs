using Sven.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Détection d'ambiguïté pilotée par l'ontologie SC4VE, bilingue (UserData.Locale) :
    /// - les exigences de paramètres sont lues depuis les restrictions OWL des commandes
    ///   (sc4ve:hasParameter / owl:onClass / owl:qualifiedCardinality) ;
    /// - les messages « manque de paramètre » sont attachés aux classes de paramètres
    ///   via sc4ve:clarification ;
    /// - le message « manque de sens » provient de sc4ve:notUnderstood.
    /// Aucun texte n'est codé en dur : tout vient du graphe de connaissance.
    /// </summary>
    public static class ClarificationVocabulary
    {
        // type de commande (nom local) → liste (classe de paramètre requise, cardinalité)
        private static Dictionary<string, List<(string ParamClass, int Cardinality)>> _requirements;
        // classe de paramètre (nom local) → question localisée
        private static Dictionary<string, string> _clarifications;
        private static string _notUnderstood;
        private static string _cachedLocale;
        private static bool _validated;

        /// <summary>Message « je n'ai pas compris » localisé (manque de sens).</summary>
        public static string NotUnderstood => _notUnderstood;

        public static async Task InitializeAsync()
        {
            string locale = UserData.Locale;
            if (_requirements != null && _cachedLocale == locale) return;

            Graph graph = new();
            Dictionary<string, string> ontologies = await SvenSettings.GetOntologiesAsync();
            TurtleParser parser = new();
            foreach (string ttl in ontologies.Values)
                parser.Load(graph, ttl);

            _requirements   = QueryRequirements(graph);
            _clarifications = QueryClarifications(graph, locale);
            _notUnderstood  = QueryNotUnderstood(graph, locale);
            _cachedLocale   = locale;

            Debug.Log($"[Clarification] {_requirements.Count} commande(s) avec exigences, " +
                      $"{_clarifications.Count} message(s) de paramètre, locale '{locale}'.");

            // Contrôle de dérive C# <-> ontologie (une seule fois ; indépendant de la locale).
            if (!_validated)
            {
                ValidateAgainstCommands(QueryCommandClasses(graph));
                _validated = true;
            }
        }

        /// <summary>
        /// Retourne la question de clarification localisée si la commande a un paramètre
        /// requis manquant (manque de paramètre), sinon null.
        /// </summary>
        public static string GetMissingParameterPrompt(Command command)
        {
            if (_requirements == null || command?.Type == null) return null;
            if (!_requirements.TryGetValue(command.Type, out var reqs)) return null;

            List<Parameter> present = command.Parameters ?? new List<Parameter>();
            foreach ((string paramClass, int cardinality) in reqs)
            {
                int satisfied = present.Count(p => p.Type == paramClass && IsSatisfied(p));
                if (satisfied < cardinality)
                    return _clarifications.TryGetValue(paramClass, out string msg) ? msg : null;
            }
            return null;
        }

        // Un SelectionParameter ne compte que si l'utilisateur a réellement spécifié un
        // critère (filtre — annotation, couleur, pointage ou coréférence) ; un SelectionParameter
        // vide signifie « cible absente ». Les autres paramètres comptent dès qu'ils sont présents.
        private static bool IsSatisfied(Parameter p)
        {
            if (p is SelectionParameter sel) return (sel.Filters?.Count ?? 0) > 0;
            return true;
        }

        private static string LocalName(string uri)
        {
            int hash = uri.LastIndexOf('#');
            if (hash >= 0) return uri.Substring(hash + 1);
            int slash = uri.LastIndexOf('/');
            return slash >= 0 ? uri.Substring(slash + 1) : uri;
        }

        private static Dictionary<string, List<(string, int)>> QueryRequirements(Graph graph)
        {
            var requirements = new Dictionary<string, List<(string, int)>>();
            const string query = @"
PREFIX owl: <http://www.w3.org/2002/07/owl#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX sc4ve: <https://sc4ve.lisn.upsaclay.fr/ontology#>
SELECT ?cmd ?param ?card WHERE {
    ?cmd rdfs:subClassOf sc4ve:Command .
    ?cmd rdfs:subClassOf ?r .
    ?r owl:onProperty sc4ve:hasParameter ;
       owl:onClass ?param ;
       owl:qualifiedCardinality ?card .
}";
            if (graph.ExecuteQuery(query) is SparqlResultSet results)
            {
                foreach (SparqlResult row in results.Cast<SparqlResult>())
                {
                    if (row["card"] is not ILiteralNode cardNode ||
                        !int.TryParse(cardNode.Value, out int card)) continue;

                    string cmd   = LocalName(row["cmd"].ToString());
                    string param = LocalName(row["param"].ToString());

                    if (!requirements.TryGetValue(cmd, out var list))
                        requirements[cmd] = list = new List<(string, int)>();
                    list.Add((param, card));
                }
            }
            return requirements;
        }

        private static Dictionary<string, string> QueryClarifications(Graph graph, string locale)
        {
            var result = new Dictionary<string, string>();
            string query = $@"
PREFIX sc4ve: <https://sc4ve.lisn.upsaclay.fr/ontology#>
SELECT ?param ?msg WHERE {{
    ?param sc4ve:clarification ?msg .
    FILTER(langMatches(lang(?msg), ""{locale}""))
}}";
            if (graph.ExecuteQuery(query) is SparqlResultSet results)
                foreach (SparqlResult row in results.Cast<SparqlResult>())
                {
                    string msg = (row["msg"] as ILiteralNode)?.Value;
                    if (msg != null) result[LocalName(row["param"].ToString())] = msg;
                }
            return result;
        }

        private static string QueryNotUnderstood(Graph graph, string locale)
        {
            string query = $@"
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX sc4ve: <https://sc4ve.lisn.upsaclay.fr/ontology#>
SELECT ?msg WHERE {{
    sc4ve:notUnderstood rdfs:label ?msg .
    FILTER(langMatches(lang(?msg), ""{locale}""))
}}";
            if (graph.ExecuteQuery(query) is SparqlResultSet results && results.Count > 0)
                return (results.Cast<SparqlResult>().First()["msg"] as ILiteralNode)?.Value;
            return null;
        }

        private static HashSet<string> QueryCommandClasses(Graph graph)
        {
            var commands = new HashSet<string>();
            const string query = @"
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX sc4ve: <https://sc4ve.lisn.upsaclay.fr/ontology#>
SELECT DISTINCT ?cmd WHERE { ?cmd rdfs:subClassOf sc4ve:Command . }";
            if (graph.ExecuteQuery(query) is SparqlResultSet results)
                foreach (SparqlResult row in results.Cast<SparqlResult>())
                    commands.Add(LocalName(row["cmd"].ToString()));
            return commands;
        }

        /// <summary>
        /// Avertit en cas de divergence entre les commandes C# et l'ontologie sc4ve, pour
        /// aider à garder sc4ve.ttl à jour (cf. COMMANDS.md, Étape 4). Sans effet fonctionnel.
        /// </summary>
        private static void ValidateAgainstCommands(HashSet<string> ontologyCommands)
        {
            // Commandes internes non destinées à l'ontologie de clarification.
            var excluded = new HashSet<string> { "UnknownCommand", "SpeechCommand" };
            // Commandes dont le SelectionParameter est volontairement optionnel
            // (« désélectionne tout » n'a pas de cible) → aucune restriction attendue.
            var optionalSelection = new HashSet<string> { "UnselectCommand" };

            var csharp = Assembly.GetAssembly(typeof(Command)).GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(Command)) && !excluded.Contains(t.Name))
                .ToList();
            var csharpNames = new HashSet<string>(csharp.Select(t => t.Name));

            // Dérive A : commande C# absente de l'ontologie → aucune clarification possible.
            foreach (var t in csharp)
                if (!ontologyCommands.Contains(t.Name))
                    Debug.LogWarning($"[Clarification] '{t.Name}' existe en C# mais n'est pas déclarée dans sc4ve.ttl. " +
                                     "Ajoute la classe + ses exigences (COMMANDS.md, Étape 4).");

            // Dérive C : classe d'ontologie sans type C# correspondant (faute de frappe / obsolète).
            foreach (string c in ontologyCommands)
                if (!csharpNames.Contains(c) && !excluded.Contains(c))
                    Debug.LogWarning($"[Clarification] sc4ve:{c} est déclarée dans sc4ve.ttl mais aucun type C# ne correspond " +
                                     "(nom erroné, ou commande supprimée ?).");

            // Dérive B : commande agissant sur la sélection par défaut mais sans aucune exigence
            // déclarée → restriction SelectionParameter probablement oubliée.
            foreach (var t in csharp)
            {
                if (!ontologyCommands.Contains(t.Name) || _requirements.ContainsKey(t.Name)
                    || optionalSelection.Contains(t.Name)) continue;
                var m = t.GetMethod("BuildRuleBasedParameters");
                if (m != null && m.DeclaringType == typeof(Command))
                    Debug.LogWarning($"[Clarification] '{t.Name}' agit sur une sélection par défaut mais ne déclare aucune " +
                                     "exigence dans sc4ve.ttl — ajoute une restriction SelectionParameter (COMMANDS.md, Étape 4).");
            }
        }
    }
}
