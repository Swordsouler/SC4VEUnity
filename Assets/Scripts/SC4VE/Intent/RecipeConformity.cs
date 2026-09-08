using Sven.Content;
using Sven.GraphManagement;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF.Query;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Vérifie qu'un contenant satisfait une recette.
    ///
    /// Tout vient du graphe : le contenu de l'assiette, les annotations de chaque ingrédient
    /// (type ET état), et les exigences de la recette elle-même, déclarées en Turtle. Rien
    /// n'est codé en dur — ni la liste des ingrédients, ni ce qu'est une viande. Ajouter
    /// « Pork » sous sven:Meat suffit pour qu'une contrainte « sans viande » le rejette.
    ///
    /// La comparaison finale se fait en C# et non en SPARQL, et c'est délibéré : la clôture
    /// (« et rien d'autre ») demanderait un FILTER NOT EXISTS imbriqué sur des exigences de
    /// cardinalité variable, illisible et intestable. Le C# ne porte ici AUCUNE connaissance
    /// du domaine — seulement de la comparaison d'ensembles.
    ///
    /// Voir Assets/Demonstration/README.md §6.3 et §6.5.
    /// </summary>
    public static class RecipeConformity
    {
        public class Requirement
        {
            public string Ingredient;
            public readonly HashSet<string> States = new();
            public readonly HashSet<string> ForbiddenStates = new();

            public override string ToString()
            {
                string states = States.Count > 0 ? " " + string.Join("+", States.Select(Local)) : "";
                string forbidden = ForbiddenStates.Count > 0
                    ? " (sans " + string.Join(", ", ForbiddenStates.Select(Local)) + ")"
                    : "";
                return Local(Ingredient) + states + forbidden;
            }
        }

        public class Report
        {
            public string Recipe;

            /// <summary>
            /// Faux quand l'ontologie ne connaît pas cette recette, ou qu'elle ne déclare aucun
            /// sven:requires. Sans ce drapeau, un rapport vide — zéro manque, zéro surplus —
            /// se lisait comme « conforme » : une recette inexistante faisait répondre « oui,
            /// c'est prêt » sur une assiette vide.
            /// </summary>
            public bool RecipeKnown = true;

            public bool IsConformant => RecipeKnown && Missing.Count == 0 && Extra.Count == 0;
            public readonly List<string> Missing = new();
            public readonly List<string> Extra = new();

            public override string ToString()
            {
                if (!RecipeKnown) return $"{Local(Recipe)} : recette inconnue de l'ontologie.";
                if (IsConformant) return $"{Local(Recipe)} : conforme.";
                var parts = new List<string>();
                if (Missing.Count > 0) parts.Add("manque " + string.Join(", ", Missing));
                if (Extra.Count > 0) parts.Add("en trop " + string.Join(", ", Extra));
                return $"{Local(Recipe)} : non conforme — {string.Join(" ; ", parts)}.";
            }
        }

        /// <param name="container">Le contenant à inspecter (une assiette).</param>
        /// <param name="recipe">Nom préfixé de la recette, par exemple « sven:FruitSalad ».</param>
        public static async Task<Report> Check(SemantizationCore container, string recipe)
        {
            var report = new Report { Recipe = recipe };
            if (container == null) return report;

            List<Requirement> requirements = await Requirements(recipe);
            if (requirements.Count == 0)
            {
                Debug.LogWarning($"[RecipeConformity] Aucune exigence pour {recipe} : recette " +
                                 "inconnue de l'ontologie, ou sven:requires absent.");
                report.RecipeKnown = false;
                return report;
            }

            Dictionary<string, HashSet<string>> content = await QueryContent(container);
            Match(content, requirements, report);
            return report;
        }

        /// <summary>
        /// Le meilleur rapport parmi plusieurs recettes candidates — les trois soupes d'une
        /// famille, par exemple. Rend le premier rapport conforme ; à défaut, le moins mauvais
        /// (manques + surplus minimaux), pour que le refus puisse être suivi du détail.
        ///
        /// UNE seule lecture du contenu pour toutes les candidates : appeler Check en boucle
        /// poserait la requête d'intervalles (LIMIT 10000) une fois par recette, sur le chemin
        /// critique de chaque service. Rend un rapport RecipeKnown == false si aucune
        /// candidate n'est connue de l'ontologie.
        /// </summary>
        public static async Task<Report> CheckAny(SemantizationCore container, IReadOnlyList<string> recipes)
        {
            if (container == null || recipes == null || recipes.Count == 0)
                return new Report { Recipe = "(aucune candidate)", RecipeKnown = false };

            Dictionary<string, HashSet<string>> content = await QueryContent(container);

            Report best = null;
            foreach (string recipe in recipes)
            {
                List<Requirement> requirements = await Requirements(recipe);
                if (requirements.Count == 0) continue;

                var report = new Report { Recipe = recipe };
                Match(content, requirements, report);
                if (report.IsConformant) return report;

                if (best == null ||
                    report.Missing.Count + report.Extra.Count < best.Missing.Count + best.Extra.Count)
                    best = report;
            }

            return best ?? new Report { Recipe = recipes[0], RecipeKnown = false };
        }

        /// <summary>
        /// Appariement glouton : chaque exigence consomme un objet distinct. Suffisant tant
        /// que deux exigences d'une même recette portent sur des ingrédients différents, ce
        /// qui est le cas des neuf recettes (§6.4). Si cela changeait, il faudrait un
        /// couplage maximal plutôt qu'un parcours simple.
        /// </summary>
        private static void Match(Dictionary<string, HashSet<string>> content,
                                  List<Requirement> requirements, Report report)
        {
            var unused = new HashSet<string>(content.Keys);

            foreach (Requirement requirement in requirements)
            {
                string match = unused.FirstOrDefault(item => Satisfies(content[item], requirement));
                if (match == null) report.Missing.Add(requirement.ToString());
                else unused.Remove(match);
            }

            // La clôture : un objet qui ne sert aucune exigence est en trop. C'est ce qui
            // distingue une salade de fruits d'une assiette contenant en plus un steak.
            foreach (string item in unused)
                report.Extra.Add(DescribeItem(content[item]));
        }

        private static bool Satisfies(HashSet<string> classes, Requirement requirement)
            => classes.Contains(requirement.Ingredient)
               && requirement.States.All(classes.Contains)
               && !requirement.ForbiddenStates.Any(classes.Contains);

        /// <summary>Les exigences de la recette, lues dans l'ontologie.</summary>
        public static async Task<List<Requirement>> Requirements(string recipe)
        {
            string query = $@"{Prefixes}
SELECT ?requirement ?ingredient ?state ?forbidden
WHERE {{
    {recipe} sven:requires ?requirement .
    ?requirement sven:ingredient ?ingredient .
    OPTIONAL {{ ?requirement sven:state ?state . }}
    OPTIONAL {{ ?requirement sven:forbidsState ?forbidden . }}
}}";

            // Sans inférence : la hiérarchie d'annotations est déjà matérialisée sur les objets
            // (l'inspecteur SVEN écrit les parents), donc le raisonneur n'apporterait rien ici
            // et coûterait cher à chaque vérification.
            SparqlResultSet results = await GraphManager.QueryMemoryAsync(query, withInference: false);

            var byNode = new Dictionary<string, Requirement>();
            foreach (SparqlResult result in results.Cast<SparqlResult>())
            {
                string node = Value(result, "requirement");
                if (node == null) continue;

                if (!byNode.TryGetValue(node, out Requirement requirement))
                    byNode[node] = requirement = new Requirement { Ingredient = Value(result, "ingredient") };

                string state = Value(result, "state");
                if (state != null) requirement.States.Add(state);

                string forbidden = Value(result, "forbidden");
                if (forbidden != null) requirement.ForbiddenStates.Add(forbidden);
            }

            return byNode.Values.Where(r => r.Ingredient != null).ToList();
        }

        /// <summary>
        /// Ce que contient le contenant maintenant, et les annotations de chaque objet.
        ///
        /// Deux intervalles distincts sont nécessaires — celui de l'appartenance au contenant
        /// et celui de l'annotation — car ils n'ont aucune raison de coïncider : une pomme est
        /// annotée depuis le début de la partie et posée dans l'assiette bien plus tard.
        /// Les confondre en une seule variable ne renverrait rien.
        /// </summary>
        private static async Task<Dictionary<string, HashSet<string>>> QueryContent(SemantizationCore container)
        {
            string containerUri = $"<{GraphManager.BaseUri}{container.GetUUID()}>";

            string query = $@"{Prefixes}
SELECT DISTINCT ?item ?class
WHERE {{
{CurrentInterval("intervalContent")}
{CurrentInterval("intervalAnnotation")}
    {containerUri} sven:component ?containerComponent .
    ?containerComponent a sven:ContainerContent ;
                        sven:content ?contentProperty .
    ?contentProperty sven:value ?item ;
                     sven:hasTemporalExtent ?intervalContent .

    ?item sven:component ?annotatorComponent .
    ?annotatorComponent a sven:Annotator ;
                        sven:annotation ?annotationProperty .
    ?annotationProperty sven:value ?class ;
                        sven:hasTemporalExtent ?intervalAnnotation .
}} LIMIT 10000";

            SparqlResultSet results = await GraphManager.QueryMemoryAsync(query, withInference: false);

            var content = new Dictionary<string, HashSet<string>>();
            foreach (SparqlResult result in results.Cast<SparqlResult>())
            {
                string item = Value(result, "item");
                string cls = Value(result, "class");
                if (item == null || cls == null) continue;

                if (!content.TryGetValue(item, out HashSet<string> classes))
                    content[item] = classes = new HashSet<string>();
                classes.Add(cls);
            }
            return content;
        }

        /// <summary>
        /// Sous-requête « intervalle valide en ce moment », nommée pour pouvoir en poser
        /// plusieurs dans la même requête sans qu'elles se contraignent l'une l'autre.
        ///
        /// L'intervalle OUVERT — celui qui court encore, donc le seul qui décrive l'état
        /// présent — se teste par l'ABSENCE de fin, jamais par une borne calculée. J'avais
        /// écrit `BIND(IF(BOUND(?end), ?end, NOW()))` puis `FILTER(NOW() < ?end)` : pour un
        /// intervalle ouvert cela revient à `NOW() < NOW()`, toujours faux. La requête ne
        /// renvoyait donc JAMAIS rien, et toute assiette paraissait vide — sans la moindre
        /// erreur.
        ///
        /// Le code de SVEN échappe au piège parce qu'il compare à un instant littéral passé,
        /// strictement antérieur à NOW() ; le recopier en remplaçant cet instant par NOW()
        /// casse l'invariant.
        /// </summary>
        private static string CurrentInterval(string variable) => $@"
    {{
        SELECT DISTINCT ?{variable}
        WHERE {{
            ?{variable} a time:Interval ;
                        time:hasBeginning/time:inXSDDateTime ?start_{variable} .
            OPTIONAL {{ ?{variable} time:hasEnd/time:inXSDDateTime ?_end_{variable} . }}
            FILTER(?start_{variable} <= NOW() && (!BOUND(?_end_{variable}) || NOW() < ?_end_{variable}))
        }} LIMIT 10000
    }}";

        private const string Prefixes = @"PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX time: <http://www.w3.org/2006/time#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>";

        private static string Value(SparqlResult result, string variable)
            => result.HasBoundValue(variable) && result[variable] != null
                ? result[variable].ToString()
                : null;

        /// <summary>Nom court d'une URI, pour des messages lisibles.</summary>
        private static string Local(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return uri;
            int cut = uri.LastIndexOfAny(new[] { '#', '/', ':' });
            return cut >= 0 && cut < uri.Length - 1 ? uri[(cut + 1)..] : uri;
        }

        /// <summary>
        /// Décrit un objet par sa classe la plus spécifique — celle qu'aucune autre annotation
        /// ne subsume. Les parents sont matérialisés sur l'objet, donc « pomme » et « fruit »
        /// et « nourriture » y figurent tous : dire « fruit en trop » serait moins utile.
        /// </summary>
        private static string DescribeItem(HashSet<string> classes)
        {
            // « Refused » y figure : sans lui, « en trop : Refused » sortirait au lieu de
            // « en trop : Banana », au hasard de l'ordre du HashSet.
            string[] generic = { "Food", "Fruit", "Vegetable", "Meat", "Fish", "Dairy", "Bakery", "FoodState", "Refused" };
            string specific = classes.Select(Local).FirstOrDefault(c => !generic.Contains(c));
            return specific ?? string.Join("/", classes.Select(Local));
        }
    }
}
