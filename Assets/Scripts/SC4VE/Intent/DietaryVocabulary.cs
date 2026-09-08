using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Query;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Le catalogue des contraintes alimentaires, lu dans l'ontologie et jamais codé.
    ///
    /// C'est ici, et nulle part ailleurs, que se fait l'inférence taxonomique du §6.5 : la
    /// fermeture des classes exclues suit rdfs:subClassOf* sur la taxonomie DÉCLARÉE, donc
    /// « pas de poisson » contient sven:Salmon sans que personne n'ait écrit ce couple — et
    /// ajouter sven:Pork sous sven:Meat fait marcher « végétarien » tout seul.
    ///
    /// Aucun fichier C# du projet ne contient les chaînes « banane », « viande », « poisson »
    /// ni aucun nom de contrainte : ajouter une septième contrainte est une édition Turtle.
    ///
    /// Tout interroge OntologyCache — le graphe ontologique statique — et non le graphe
    /// d'exécution : appelé au Start d'un client, potentiellement avant l'initialisation de
    /// GraphManager, et sur des données qui sont des constantes.
    /// </summary>
    public static class DietaryVocabulary
    {
        public readonly struct Constraint
        {
            /// <summary>Nom PRÉFIXÉ, par exemple « sven:NoBanana ».</summary>
            public readonly string Uri;

            /// <summary>Libellé prononcé, par exemple « sans banane ».</summary>
            public readonly string Label;

            public Constraint(string uri, string label)
            {
                Uri = uri;
                Label = label;
            }
        }

        private const string SvenNamespace = "https://sven.lisn.upsaclay.fr/ontology#";

        private const string Prefixes = @"PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
";

        /// <summary>
        /// La contrainte alimentaire portée par cet ensemble d'annotations, ou null.
        ///
        /// La requête EXIGE sven:excludes dans son corps : une contrainte déclarée sans lui ne
        /// refuserait rien tout en paraissant active — elle serait annoncée par le client,
        /// affichée au tableau, et n'agirait jamais. Ici, elle n'est simplement jamais rendue,
        /// et le test EditMode EveryDietaryConstraintExcludesSomething signale l'oubli.
        /// </summary>
        public static async Task<Constraint?> ConstraintOfAsync(IEnumerable<string> annotations, string locale)
        {
            string values = Values(annotations);
            if (values == null) return null;

            Graph graph = await OntologyCache.GetGraphAsync();
            string query = $@"{Prefixes}
SELECT DISTINCT ?constraint ?label
WHERE {{
    VALUES ?constraint {{ {values} }}
    ?constraint rdfs:subClassOf sven:DietaryConstraint ;
                sven:excludes   ?excluded ;
                rdfs:label      ?label .
    FILTER(langMatches(lang(?label), ""{locale}""))
}} ORDER BY ?constraint ?label";

            if (graph.ExecuteQuery(query) is not SparqlResultSet results) return null;

            var found = new List<Constraint>();
            foreach (SparqlResult result in results.Cast<SparqlResult>())
            {
                string uri = ToPrefixed(result["constraint"]?.ToString());
                string label = (result["label"] as ILiteralNode)?.Value;
                if (uri == null || string.IsNullOrWhiteSpace(label)) continue;
                if (found.Any(c => c.Uri == uri)) continue; // un libellé par contrainte (ORDER BY → déterministe)
                found.Add(new Constraint(uri, label));
            }

            if (found.Count > 1)
                Debug.LogWarning("[DietaryVocabulary] Plusieurs contraintes sur le même client (" +
                                 string.Join(", ", found.Select(c => c.Uri)) +
                                 ") : seule la première est annoncée, mais TOUTES excluent — " +
                                 "la fermeture les cumule.");

            return found.Count > 0 ? found[0] : null;
        }

        /// <summary>
        /// La fermeture DESCENDANTE des classes exclues par ces annotations, en noms PRÉFIXÉS,
        /// directement comparables à SemanticAnnotator.Annotations.
        ///
        /// rdfs:subClassOf* et non + : « sans banane » exclut une FEUILLE, et sans le chemin de
        /// longueur nulle la classe exclue elle-même sortirait de la fermeture — la contrainte
        /// ne rejetterait rien du tout. (Si le moteur SPARQL refusait « * », remplacer par
        /// l'union « BIND(?excluded AS ?class) » ∪ « ?class rdfs:subClassOf+ ?excluded » ; la
        /// garde de CustomerOrder.Start — fermeture vide malgré une contrainte — le détecte au
        /// premier client, avant la démonstration.)
        ///
        /// Contient aussi sven:Refused dès que sven:Customer figure dans les annotations, par
        /// la même requête et sans une ligne de code de plus : c'est la règle universelle
        /// « personne ne remange un plat refusé », déclarée en Turtle sur sven:Customer.
        /// </summary>
        public static async Task<HashSet<string>> ExcludedClassesAsync(IEnumerable<string> annotations)
        {
            var excluded = new HashSet<string>();
            string values = Values(annotations);
            if (values == null) return excluded;

            Graph graph = await OntologyCache.GetGraphAsync();
            string query = $@"{Prefixes}
SELECT DISTINCT ?class
WHERE {{
    VALUES ?type {{ {values} }}
    ?type  sven:excludes    ?excluded .
    ?class rdfs:subClassOf* ?excluded .
}}";

            if (graph.ExecuteQuery(query) is SparqlResultSet results)
                foreach (SparqlResult result in results.Cast<SparqlResult>())
                {
                    string uri = ToPrefixed(result["class"]?.ToString());
                    if (uri != null) excluded.Add(uri);
                }

            // Invariant d'URI : tout ce qui sort d'ici DOIT être préfixé, parce que c'est la
            // forme que SemanticAnnotator.Annotations contient. Une URI complète comparée à
            // « sven:Banana » ne lève rien : l'intersection est vide, le client accepte tout,
            // et le critère 4 échoue EN PARAISSANT RÉUSSIR.
            if (excluded.Count > 0 && !excluded.Any(c => c.StartsWith("sven:")))
                Debug.LogError("[DietaryVocabulary] La fermeture des classes exclues ne contient " +
                               "aucun nom préfixé « sven: » : l'intersection avec les annotations " +
                               "sera toujours vide et aucune contrainte n'agira. Fermeture : " +
                               string.Join(", ", excluded));

            return excluded;
        }

        /// <summary>
        /// La clause VALUES construite depuis les annotations du client — uniquement les noms
        /// préfixés sven:, seuls interrogeables sans résolution d'espace de noms.
        /// </summary>
        private static string Values(IEnumerable<string> annotations)
        {
            List<string> names = annotations?
                .Where(a => !string.IsNullOrWhiteSpace(a) && a.StartsWith("sven:") && !a.Contains(' '))
                .Distinct()
                .ToList();
            return names is { Count: > 0 } ? string.Join(" ", names) : null;
        }

        private static string ToPrefixed(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return null;
            return uri.StartsWith(SvenNamespace) ? "sven:" + uri[SvenNamespace.Length..] : uri;
        }
    }
}
