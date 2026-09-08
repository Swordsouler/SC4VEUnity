using NUnit.Framework;
using Sc4ve.Multimodality;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;

namespace Sc4ve.Tests.EditMode
{
    /// <summary>
    /// Les invariants du lot 4 que rien ne signalerait à l'exécution : une contrainte sans
    /// sven:excludes est annoncée et n'agit pas, une classe exclue mal orthographiée rend la
    /// contrainte inerte, une étape de client renommée d'un seul côté produit des URI que
    /// personne n'a déclarées — tous des échecs parfaitement silencieux.
    ///
    /// Le graphe est reconstruit depuis TOUS les .ttl de StreamingAssets/Ontologies, comme le
    /// fait GraphManager : sven:Banana vit dans sven-fruits.ttl, pas dans sven-restaurant.ttl,
    /// et un test qui ne chargerait que ce dernier échouerait sur la contrainte exacte que le
    /// critère 4 met en scène.
    /// </summary>
    public class RestaurantOntologyConsistencyTests
    {
        private const string Prefixes = @"PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX owl: <http://www.w3.org/2002/07/owl#>
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
";

        private static Graph _graph;

        private static Graph Load()
        {
            if (_graph != null) return _graph;

            string folder = Path.Combine(Application.streamingAssetsPath, "Ontologies");
            string[] files = Directory.GetFiles(folder, "*.ttl");
            Assert.IsNotEmpty(files, $"Aucune ontologie dans {folder} : le test ne vérifie rien.");

            var graph = new Graph();
            var parser = new TurtleParser();
            foreach (string file in files)
            {
                // Un .ttl illisible doit nommer SON fichier : cinq fichiers sont fusionnés, et
                // l'erreur de parse brute ne dit pas lequel est en faute.
                try
                {
                    parser.Load(graph, file);
                }
                catch (Exception e)
                {
                    Assert.Fail($"{Path.GetFileName(file)} ne parse pas : {e.Message}");
                }
            }

            return _graph = graph;
        }

        private static List<string> Rows(string query, string variable)
        {
            return Load().ExecuteQuery(Prefixes + query) is SparqlResultSet results
                ? results.Cast<SparqlResult>()
                    .Select(r => r.HasBoundValue(variable) ? r[variable]?.ToString() : null)
                    .Where(v => v != null)
                    .ToList()
                : new List<string>();
        }

        private static bool Ask(string query)
            => Load().ExecuteQuery(Prefixes + query) is SparqlResultSet { Result: true };

        [Test]
        public void OntologiesParse()
        {
            Assert.Greater(Load().Triples.Count, 0);
        }

        /// <summary>
        /// CustomerOrder.Stage construit ses URI par concaténation (« sven: » + valeur) et
        /// CreateUriNode ne valide rien : renommer une étape d'un seul côté remplit le graphe
        /// d'URI que personne n'a déclarées, sans la moindre erreur.
        /// </summary>
        [Test]
        public void EveryCustomerStageHasAnIndividual()
        {
            foreach (string stage in Enum.GetNames(typeof(CustomerOrder.Stage)))
                Assert.IsTrue(
                    Ask($"ASK {{ sven:{stage} rdf:type sven:CustomerActivity . }}"),
                    $"sven:{stage} n'est pas un individu sven:CustomerActivity : l'étape C# " +
                    $"« {stage} » produirait des URI non déclarées, et toute requête sur " +
                    "l'état des clients cesserait de répondre.");
        }

        /// <summary>
        /// Une contrainte sans sven:excludes serait annoncée par le client, affichée au
        /// tableau, et n'agirait jamais. Et une contrainte sans label dans une locale rendrait
        /// le client muet dans cette langue.
        /// </summary>
        [Test]
        public void EveryDietaryConstraintExcludesSomething()
        {
            List<string> constraints = Rows(
                "SELECT DISTINCT ?c WHERE { ?c rdfs:subClassOf sven:DietaryConstraint . }", "c");
            Assert.IsNotEmpty(constraints, "Aucune contrainte alimentaire déclarée : le §6.5 est vide.");

            foreach (string constraint in constraints)
            {
                string name = constraint.Split('#').Last();
                Assert.IsTrue(Ask($"ASK {{ sven:{name} sven:excludes ?e . }}"),
                    $"sven:{name} ne déclare aucun sven:excludes : elle serait annoncée et " +
                    "n'agirait jamais.");
                foreach (string locale in new[] { "fr", "en" })
                    Assert.IsTrue(
                        Ask($"ASK {{ sven:{name} rdfs:label ?l . FILTER(langMatches(lang(?l), \"{locale}\")) }}"),
                        $"sven:{name} n'a pas de rdfs:label @{locale}.");
            }
        }

        /// <summary>
        /// La règle universelle « personne ne remange un plat refusé ». La supprimer fait
        /// tomber le critère 5 — le coût du refus — sans le moindre message.
        /// </summary>
        [Test]
        public void CustomerExcludesRefused()
        {
            Assert.IsTrue(Ask("ASK { sven:Customer sven:excludes sven:Refused . }"),
                "sven:Customer sven:excludes sven:Refused a disparu : un plat refusé " +
                "redeviendrait servable, et vider à la poubelle ne servirait plus à rien.");
        }

        /// <summary>
        /// Un sven:excludes vers une classe inexistante (« sven:Bananna ») rend la contrainte
        /// parfaitement inerte : la fermeture descendante ne contient que la faute de frappe,
        /// qu'aucun aliment ne porte.
        /// </summary>
        [Test]
        public void EveryExcludedClassIsDeclared()
        {
            List<string> excluded = Rows(
                "SELECT DISTINCT ?e WHERE { ?c sven:excludes ?e . }", "e");
            Assert.IsNotEmpty(excluded, "Aucun sven:excludes : les contraintes n'existent pas.");

            foreach (string cls in excluded)
            {
                string name = cls.Split('#').Last();
                Assert.IsTrue(Ask($"ASK {{ sven:{name} rdf:type owl:Class . }}"),
                    $"sven:excludes pointe {cls}, qui n'est déclaré owl:Class nulle part : " +
                    "la contrainte qui l'exclut est inerte, sans erreur.");
            }
        }

        /// <summary>
        /// Les quatre couples (famille, contrainte) écrits par DemoSceneBuilder doivent tous
        /// laisser AU MOINS un plat acceptable — un couple dégénéré (Vegan + Sandwich : zéro
        /// plat) ferait tourner le joueur en rond sans explication. La colonne « plats
        /// acceptables » du §6.5 n'est écrite nulle part : ce test la CALCULE, comme le client.
        /// </summary>
        [Test]
        public void EveryBuilderCoupleLeavesAnAcceptableDish()
        {
            (string family, string constraint)[] couples =
            {
                ("Salad", "NoBanana"),
                ("Soup", "FishAllergy"),
                ("Salad", "Vegetarian"),
                ("Sandwich", "LactoseFree"),
            };

            foreach ((string family, string constraint) in couples)
            {
                var excluded = new HashSet<string>(Rows(
                    $"SELECT DISTINCT ?cl WHERE {{ sven:{constraint} sven:excludes ?e . " +
                    "?cl rdfs:subClassOf* ?e . }", "cl"));

                List<string> children = Rows(
                    $"SELECT DISTINCT ?child WHERE {{ ?child rdfs:subClassOf* sven:{family} ; " +
                    "sven:requires ?r . }", "child");
                Assert.IsNotEmpty(children, $"sven:{family} n'a aucune recette concrète.");

                bool anyAcceptable = children.Any(child =>
                {
                    string name = child.Split('#').Last();
                    var ingredients = new HashSet<string>(Rows(
                        $"SELECT DISTINCT ?i WHERE {{ sven:{name} sven:requires ?r . " +
                        "?r sven:ingredient ?i . }", "i"));
                    return !ingredients.Overlaps(excluded);
                });

                Assert.IsTrue(anyAcceptable,
                    $"Le couple ({family}, {constraint}) ne laisse AUCUN plat acceptable : " +
                    "changer le couple dans DemoSceneBuilder.BuildDiningRoom.");
            }
        }
    }
}
