using Newtonsoft.Json;
using NUnit.Framework;
using Sc4ve.Multimodality;
using Sc4ve.Multimodality.Intent;
using Sc4ve.Multimodality.Intent.RuleBased;
using Sc4ve.Voice;
using Sven.Context;
using System.Collections.Generic;
using System.Linq;

namespace Sc4ve.Tests.EditMode
{
    // Améliorations d'extraction du mode règles issues du benchmark d'intention :
    // accord féminin des couleurs, disjonction OU/OR, tri ordinal superlatif,
    // limite déictique, ordre des filtres (Event en dernier), triggers « relâche »/« make ».
    public class RuleBasedExtractionTests
    {
        private RuleBasedIntentRecognizer _recognizer;
        private Language _previousLanguage;

        [SetUp]
        public void SetUp()
        {
            _previousLanguage = UserData.Language;
            SetLocale(Language.French);
            _recognizer = MakeRecognizer(Language.French);
        }

        [TearDown]
        public void TearDown() => UserData.Language = _previousLanguage;

        // Bascule la locale ET resynchronise le vocabulaire de triggers (mis en cache par locale).
        private static void SetLocale(Language language)
        {
            UserData.Language = language;
            Sven.Utils.SvenSettings.CacheMainThreadPaths();
            EditModeSync.RunSync(() => CommandVocabulary.InitializeAsync());
        }

        private static RuleBasedIntentRecognizer MakeRecognizer(
            Language language,
            List<string> annotationTypes = null,
            List<RecipeVocabulary.Recipe> recipes = null)
        {
            bool fr = language == Language.French;
            return new RuleBasedIntentRecognizer(
                annotationTypes: annotationTypes
                    ?? (fr ? new List<string> { "Pomme", "Banane" } : new List<string> { "Apple", "Pumpkin" }),
                availableColors: fr ? new List<string> { "Rouge", "Vert" } : new List<string> { "Red", "Blue" },
                pointerDeictics: fr
                    ? new List<string> { "ce", "ceci", "ces", "cet", "cette", "ça" }
                    : new List<string> { "this", "that", "these", "those" },
                pointerName: fr ? "Pointeur" : "Pointer",
                cameraName: fr ? "Caméra" : "Camera",
                recipes: recipes);
        }

        /// <summary>Toutes les conditions de tous les SelectionParameter de la commande.</summary>
        private static List<Condition> AllConditions(Command command)
            => command.Parameters
                .OfType<SelectionParameter>()
                .SelectMany(p => p.Filters ?? new List<FilterElement>())
                .Where(f => !f.IsOperator && f.Condition != null)
                .Select(f => f.Condition)
                .ToList();

        private T RecognizeSingle<T>(string phrase) where T : Command
        {
            string json = _recognizer.Recognize(new Sentence(phrase));
            Assert.IsNotNull(json, $"Aucune commande reconnue pour « {phrase} ».");
            List<Command> commands = JsonConvert.DeserializeObject<List<Command>>(json);
            Assert.AreEqual(1, commands.Count);
            Assert.IsInstanceOf<T>(commands[0], $"Type inattendu pour « {phrase} » : {commands[0].Type}");
            return (T)commands[0];
        }

        private static SelectionParameter Selection(Command command)
            => command.Parameters.OfType<SelectionParameter>().First();

        // ─────────────────────────────────────────────────────────────────────
        // Accord féminin des couleurs
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void FeminineColorAgreement_IsDetectedAsSourceFilter()
        {
            ColorizeCommand cmd = RecognizeSingle<ColorizeCommand>("colorie en rouge cette pomme verte");
            // « verte » (accord féminin de « vert ») doit produire un filtre Color source.
            Assert.IsTrue(Selection(cmd).Filters.Any(f => !f.IsOperator && f.Condition.Type == "Color" && f.Condition.Value == "Vert"),
                "« verte » doit être reconnue comme la couleur source « Vert ».");
            // La couleur cible reste « Rouge » (ColorParameter).
            Assert.AreEqual("Rouge", cmd.Parameters.OfType<ColorParameter>().First().Value);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Disjonction « ou » / « or »
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Disjunction_JoinsAnnotationsWithOr()
        {
            ShowCommand cmd = RecognizeSingle<ShowCommand>("montre les pommes ou les bananes");
            List<FilterElement> filters = Selection(cmd).Filters;
            Assert.AreEqual(3, filters.Count);
            Assert.AreEqual("Pomme", filters[0].Condition.Value);
            Assert.IsTrue(filters[1].IsOperator && filters[1].Operator == "OR",
                "« ou » doit produire un opérateur OR entre les annotations.");
            Assert.AreEqual("Banane", filters[2].Condition.Value);
        }

        [Test]
        public void Conjunction_KeepsAndByDefault()
        {
            HideCommand cmd = RecognizeSingle<HideCommand>("masque la pomme rouge");
            List<FilterElement> filters = Selection(cmd).Filters;
            Assert.IsTrue(filters.Any(f => f.IsOperator && f.Operator == "AND"));
            Assert.IsFalse(filters.Any(f => f.IsOperator && f.Operator == "OR"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Tri ordinal superlatif
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Superlative_SmallestProducesAscendingSizeOrder()
        {
            SelectCommand cmd = RecognizeSingle<SelectCommand>("sélectionne les 3 plus petites pommes");
            SelectionParameter selection = Selection(cmd);
            Assert.AreEqual(3, selection.Limit);
            Assert.IsNotNull(selection.Order, "« les plus petites » doit produire un tri.");
            Assert.AreEqual("size", selection.Order.Criterias[0].Type);
            Assert.IsFalse(selection.Order.Criterias[0].Desc);
        }

        [Test]
        public void Superlative_BiggestProducesDescendingSizeOrder()
        {
            SelectCommand cmd = RecognizeSingle<SelectCommand>("sélectionne les plus grosses pommes");
            SelectionParameter selection = Selection(cmd);
            Assert.IsNotNull(selection.Order);
            Assert.IsTrue(selection.Order.Criterias[0].Desc);
        }

        [Test]
        public void NoSuperlative_ProducesNoOrder()
        {
            SelectCommand cmd = RecognizeSingle<SelectCommand>("sélectionne les pommes");
            Assert.IsNull(Selection(cmd).Order);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Limite déictique + ordre des filtres
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void DeicticSingular_LimitsToOne_AndPutsEventLast()
        {
            HideCommand cmd = RecognizeSingle<HideCommand>("masque cette banane");
            SelectionParameter selection = Selection(cmd);
            Assert.AreEqual(1, selection.Limit, "Une référence pointée au singulier cible UN objet.");
            // Convention d'ordre : annotation d'abord, pointage (Event) en dernier.
            Assert.AreEqual("Annotation", selection.Filters[0].Condition.Type);
            Assert.AreEqual("Event", selection.Filters[^1].Condition.Type);
        }

        [Test]
        public void DeicticPlural_KeepsAllMatches()
        {
            HideCommand cmd = RecognizeSingle<HideCommand>("cache ces pommes");
            // « ces » est pluriel : pas de restriction à un objet.
            Assert.AreEqual(-1, Selection(cmd).Limit);
        }

        [Test]
        public void DescriptiveSingular_KeepsMinusOneForDisambiguation()
        {
            HideCommand cmd = RecognizeSingle<HideCommand>("masque la banane");
            // Sans pointage, limit reste -1 : la désambiguïsation (« laquelle ? ») doit pouvoir
            // s'exécuter — un LIMIT 1 SPARQL prendrait un objet arbitraire.
            SelectionParameter selection = Selection(cmd);
            Assert.AreEqual(-1, selection.Limit);
            Assert.IsTrue(selection.SingularIntent);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Couverture lexicale des triggers
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Relache_TriggersReleaseCommand()
        {
            RecognizeSingle<ReleaseCommand>("relâche");
        }

        [Test]
        public void English_MakeColorPattern_TriggersColorize()
        {
            // Bascule complète en anglais (locale + vocabulaire de triggers + recognizer).
            try
            {
                SetLocale(Language.English);
                _recognizer = MakeRecognizer(Language.English);
                ColorizeCommand cmd = RecognizeSingle<ColorizeCommand>("make the pumpkins blue");
                Assert.AreEqual("Blue", cmd.Parameters.OfType<ColorParameter>().First().Value);
                Assert.IsTrue(Selection(cmd).Filters.Any(f => !f.IsOperator && f.Condition.Value == "Pumpkin"));
            }
            finally
            {
                SetLocale(Language.French);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Libellés emboîtés : le plus précis gagne, et consomme son segment
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void NestedLabel_LongestWins_AndDoesNotAlsoMatchTheShorterOne()
        {
            _recognizer = MakeRecognizer(
                Language.French,
                annotationTypes: new List<string> { "Pomme", "Pomme de terre", "Assiette" });

            PutInCommand cmd = RecognizeSingle<PutInCommand>(
                "mets la pomme de terre dans une assiette");

            List<Condition> conditions = AllConditions(cmd);
            Assert.IsTrue(conditions.Any(c => c.Value == "Pomme de terre"),
                "« pomme de terre » doit produire l'annotation Pomme de terre.");
            Assert.IsFalse(conditions.Any(c => c.Value == "Pomme"),
                "« pomme de terre » ne doit PAS produire aussi l'annotation Pomme : " +
                "la sélection ramasserait tous les fruits de la table.");
        }

        [Test]
        public void NestedLabel_ShorterStillMatchesItsOwnOccurrence()
        {
            _recognizer = MakeRecognizer(
                Language.French,
                annotationTypes: new List<string> { "Pomme", "Pomme de terre" });

            // La consommation ne retire QUE la première occurrence : la pomme conserve la sienne.
            SelectCommand cmd = RecognizeSingle<SelectCommand>(
                "sélectionne la pomme de terre et la pomme");

            List<Condition> conditions = AllConditions(cmd);
            Assert.IsTrue(conditions.Any(c => c.Value == "Pomme de terre"));
            Assert.IsTrue(conditions.Any(c => c.Value == "Pomme"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Recettes : verbe générique + nom de plat → PrepareCommand
        // ─────────────────────────────────────────────────────────────────────

        private static List<RecipeVocabulary.Recipe> Recipes() => new()
        {
            new RecipeVocabulary.Recipe("sven:Soup",       "Soupe",             isConcrete: false),
            new RecipeVocabulary.Recipe("sven:CarrotSoup", "Soupe de carottes", isConcrete: true),
            new RecipeVocabulary.Recipe("sven:FruitSalad", "Salade de fruits",  isConcrete: true),
        };

        [Test]
        public void GenericMakeVerb_WithRecipe_TriggersPrepare()
        {
            _recognizer = MakeRecognizer(Language.French, recipes: Recipes());

            // « fais » n'est pas un déclencheur de PrepareCommand : c'est la présence du nom de
            // plat qui décide. Sans cette règle, « fais » et « make » entraient en collision
            // avec ColorizeCommand et la commande retenue dépendait de l'ordre de réflexion.
            PrepareCommand cmd = RecognizeSingle<PrepareCommand>("fais une salade de fruits");
            Assert.AreEqual("sven:FruitSalad",
                cmd.Parameters.OfType<RecipeParameter>().First().Value);
        }

        [Test]
        public void GenericMakeVerb_WithoutRecipe_DoesNotTriggerPrepare()
        {
            _recognizer = MakeRecognizer(Language.French, recipes: Recipes());

            // « fais » seul ne nomme aucune recette : la phrase ne doit pas devenir un
            // PrepareCommand qui demanderait « quelle recette ? ».
            string json = _recognizer.Recognize(new Sentence("fais la pomme"));
            if (json != null)
            {
                List<Command> commands = JsonConvert.DeserializeObject<List<Command>>(json);
                Assert.IsFalse(commands.Any(c => c is PrepareCommand),
                    "Sans nom de plat, « fais » ne doit pas produire un PrepareCommand.");
            }
        }

        [Test]
        public void PreciseRecipe_WinsOverItsFamily()
        {
            _recognizer = MakeRecognizer(Language.French, recipes: Recipes());

            PrepareCommand cmd = RecognizeSingle<PrepareCommand>("prépare une soupe de carottes");
            Assert.AreEqual("sven:CarrotSoup",
                cmd.Parameters.OfType<RecipeParameter>().First().Value,
                "« soupe de carottes » doit l'emporter sur la famille « soupe ».");
        }
    }
}
