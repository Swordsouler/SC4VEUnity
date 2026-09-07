using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sc4ve.Tests.EditMode
{
    /// <summary>
    /// Interdit qu'un même nom de type existe à la fois dans Sc4ve.Multimodality et dans
    /// Sc4ve.Demonstration.
    ///
    /// Ce n'est pas une règle de style. Le code d'outillage vit dans
    /// Sc4ve.Demonstration.EditorTools, et C# résout un nom simple en examinant les espaces de
    /// noms ENGLOBANTS avant les directives using : depuis là, « Waiter » désignait
    /// Sc4ve.Demonstration.Waiter — le marqueur d'annotation — et non
    /// Sc4ve.Multimodality.Waiter, la machine à états. DemoSceneBuilder posait donc le mauvais
    /// composant sur chaque serveur. Ça compilait sans un mot, et toute délégation répondait
    /// « Quel serveur ? » : le lot 3 entier était inerte.
    ///
    /// Aucun compilateur ne signale cette classe d'erreur, et aucune relecture ne la voit —
    /// les deux lignes sont identiques. Seule l'absence de collision la rend impossible.
    /// </summary>
    public class NamespaceCollisionTests
    {
        private const string Domain = "Sc4ve.Demonstration";
        private const string Runtime = "Sc4ve.Multimodality";

        [Test]
        public void NoTypeNameIsSharedBetweenDomainAndRuntimeNamespaces()
        {
            HashSet<string> domain = TypeNamesIn(Domain);
            HashSet<string> runtime = TypeNamesIn(Runtime);

            // Les deux ensembles doivent être non vides, sinon le test passerait pour la
            // mauvaise raison — une assembly non chargée le rendrait vert sans rien vérifier.
            Assert.IsNotEmpty(domain, $"Aucun type trouvé dans {Domain} : le test ne vérifie rien.");
            Assert.IsNotEmpty(runtime, $"Aucun type trouvé dans {Runtime} : le test ne vérifie rien.");

            List<string> collisions = domain.Intersect(runtime).OrderBy(n => n).ToList();

            Assert.IsEmpty(collisions,
                $"Noms de types partagés entre {Domain} et {Runtime} : {string.Join(", ", collisions)}. " +
                "Depuis un fichier de Sc4ve.Demonstration.*, le nom simple désignera celui du " +
                "domaine, silencieusement. Renommer l'un des deux.");
        }

        /// <summary>
        /// Les noms de types déclarés EXACTEMENT dans cet espace de noms — pas dans ses
        /// sous-espaces, qui ne créent pas d'ambiguïté entre eux.
        /// </summary>
        private static HashSet<string> TypeNamesIn(string ns)
        {
            var names = new HashSet<string>();

            foreach (Type type in AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeTypes))
                if (type.Namespace == ns && !type.IsNested)
                    names.Add(type.Name);

            return names;
        }

        private static IEnumerable<Type> SafeTypes(System.Reflection.Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
            catch { return Array.Empty<Type>(); }
        }
    }
}
