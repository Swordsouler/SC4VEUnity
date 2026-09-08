using Newtonsoft.Json;
using Sven.GraphManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Query;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Une recette nommée — « salade de fruits », « soupe de carottes ».
    ///
    /// C'est le seul paramètre purement linguistique du système : aucun pointage ne peut le
    /// fournir, puisqu'une recette n'a pas d'existence dans la scène. Il fait contrepoids aux
    /// paramètres déictiques, et c'est ce qui rend « prépare une salade de fruits » impossible
    /// à exprimer autrement qu'à la voix.
    /// </summary>
    [Serializable]
    public class RecipeParameter : Parameter
    {
        [SerializeField] private string _value;

        /// <summary>Nom préfixé de la recette, par exemple « sven:FruitSalad ».</summary>
        [JsonProperty("value")]
        public string Value
        {
            get => _value;
            set => _value = value;
        }

        public override string ToString() => _value;

        /// <summary>
        /// La recette est écrite dans la trace comme une URI et non comme une chaîne : c'est la
        /// classe même de l'ontologie (sven:FruitSalad), donc une requête d'analyse peut la
        /// joindre à ses sven:requires. Un littéral serait opaque.
        ///
        /// Le repli sur un littéral couvre le cas d'un LLM qui renvoie un nom mal formé
        /// (« FruitSalad » sans préfixe, ou un préfixe inconnu) : CreateUriNode lèverait alors
        /// une RdfException qui avorterait la sémantisation de toute la commande.
        /// </summary>
        public override async Task<IUriNode> Semanticize(Graph graph)
        {
            IUriNode parameterNode = await base.Semanticize(graph);
            if (string.IsNullOrWhiteSpace(_value)) return parameterNode;

            IUriNode value = graph.CreateUriNode("sven:value");
            INode recipe;
            try
            {
                recipe = graph.CreateUriNode(_value);
            }
            catch (RdfException)
            {
                Debug.LogWarning($"[RecipeParameter] « {_value} » n'est pas un nom préfixé valide, " +
                                 "écrit comme littéral dans la trace.");
                recipe = graph.CreateLiteralNode(_value, graph.CreateUriNode("xsd:string").Uri);
            }
            graph.Assert(new Triple(parameterNode, value, recipe));
            return parameterNode;
        }
    }

    /// <summary>
    /// Le catalogue des recettes, lu dans l'ontologie et non codé en dur : ajouter une recette
    /// au fichier Turtle suffit pour que le joueur puisse la demander.
    /// </summary>
    public static class RecipeVocabulary
    {
        public readonly struct Recipe
        {
            public readonly string Uri;
            public readonly string Label;

            /// <summary>Vrai pour une recette concrète (elle porte des sven:requires),
            /// faux pour une famille comme « soupe », qui ne fait que regrouper.</summary>
            public readonly bool IsConcrete;

            public Recipe(string uri, string label, bool isConcrete)
            {
                Uri = uri;
                Label = label;
                IsConcrete = isConcrete;
            }
        }

        /// <summary>
        /// Toutes les classes sous sven:Dish portant un label dans la locale demandée.
        /// Les familles (Salad, Soup, Sandwich) sont incluses : elles permettent les commandes
        /// sous-spécifiées (« je voudrais une soupe »), qui doivent déclencher une clarification
        /// plutôt qu'un choix arbitraire (§6.4 du README).
        /// </summary>
        public static async Task<List<Recipe>> GetAvailableRecipesAsync(string locale)
        {
            string query = $@"PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

SELECT DISTINCT ?dish ?label
WHERE {{
    ?dish rdfs:subClassOf+ sven:Dish ;
          rdfs:label ?label .
    FILTER(langMatches(lang(?label), ""{locale}""))
}}";

            var recipes = new List<Recipe>();
            try
            {
                SparqlResultSet results = await GraphManager.QueryMemoryAsync(query, withInference: false);
                foreach (SparqlResult result in results.Cast<SparqlResult>())
                {
                    string uri = result["dish"]?.ToString();
                    string label = (result["label"] as VDS.RDF.ILiteralNode)?.Value;
                    if (uri == null || string.IsNullOrWhiteSpace(label)) continue;

                    recipes.Add(new Recipe(ToPrefixed(uri), label, await HasRequirements(uri)));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RecipeVocabulary] Recettes indisponibles : {e.Message}");
            }
            return recipes;
        }

        private static async Task<bool> HasRequirements(string uri)
        {
            string query = $@"PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
ASK {{ <{uri}> sven:requires ?requirement . }}";

            SparqlResultSet results = await GraphManager.QueryMemoryAsync(query, withInference: false);
            return results != null && results.Result;
        }

        /// <summary>
        /// Vrai si <paramref name="child"/> est une sous-classe de <paramref name="parent"/> —
        /// « soupe de carottes » sous « soupe ». Sert à lister les recettes d'une famille quand
        /// la demande est sous-spécifiée.
        /// </summary>
        public static async Task<bool> IsSubClassOf(string child, string parent)
        {
            string query = $@"PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
ASK {{ {child} rdfs:subClassOf+ {parent} . }}";

            SparqlResultSet results = await GraphManager.QueryMemoryAsync(query, withInference: false);
            return results != null && results.Result;
        }

        /// <summary>
        /// Les recettes concrètes d'une famille — les trois soupes, par exemple — en URI
        /// préfixées, dédoublonnées et triées.
        ///
        /// Lue dans le graphe ONTOLOGIQUE (OntologyCache) et non dans le graphe d'exécution :
        /// appelée au Start d'un client, potentiellement avant l'initialisation de
        /// GraphManager — GetAvailableRecipesAsync avalerait l'exception et rendrait une liste
        /// vide, sans erreur, et le client n'aurait jamais de commande.
        ///
        /// SELECT DISTINCT ?child SANS le label : sven:PumpkinSoup porte deux rdfs:label@fr,
        /// et joindre le label dupliquerait la recette. Le motif sven:requires écarte la
        /// famille elle-même, qui n'en déclare aucun.
        /// </summary>
        public static async Task<List<string>> ConcreteChildrenAsync(string family)
        {
            var children = new List<string>();
            if (string.IsNullOrWhiteSpace(family) || !family.StartsWith("sven:")) return children;

            VDS.RDF.Graph graph = await OntologyCache.GetGraphAsync();
            string query = $@"PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

SELECT DISTINCT ?child
WHERE {{
    ?child rdfs:subClassOf* {family} ;
           sven:requires    ?requirement .
}} ORDER BY ?child";

            if (graph.ExecuteQuery(query) is SparqlResultSet results)
                foreach (SparqlResult result in results.Cast<SparqlResult>())
                {
                    string uri = result["child"]?.ToString();
                    if (uri != null) children.Add(ToPrefixed(uri));
                }
            return children;
        }

        /// <summary>
        /// Les classes d'ingrédients qu'une recette exige, en URI préfixées — la forme que
        /// contiennent SemanticAnnotator.Annotations et la fermeture des classes exclues.
        /// C'est ce qui permet de calculer les « plats acceptables » d'un client par simple
        /// intersection d'ensembles ; RecipeConformity.Requirements ne convient pas ici : il
        /// rend des URI complètes et interroge le graphe d'exécution.
        /// </summary>
        public static async Task<HashSet<string>> RequiredIngredientClassesAsync(string recipe)
        {
            var ingredients = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(recipe) || !recipe.StartsWith("sven:")) return ingredients;

            VDS.RDF.Graph graph = await OntologyCache.GetGraphAsync();
            string query = $@"PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>

SELECT DISTINCT ?ingredient
WHERE {{
    {recipe} sven:requires ?requirement .
    ?requirement sven:ingredient ?ingredient .
}}";

            if (graph.ExecuteQuery(query) is SparqlResultSet results)
                foreach (SparqlResult result in results.Cast<SparqlResult>())
                {
                    string uri = result["ingredient"]?.ToString();
                    if (uri != null) ingredients.Add(ToPrefixed(uri));
                }
            return ingredients;
        }

        private static string ToPrefixed(string uri)
        {
            const string svenNamespace = "https://sven.lisn.upsaclay.fr/ontology#";
            return uri.StartsWith(svenNamespace) ? "sven:" + uri[svenNamespace.Length..] : uri;
        }
    }
}
