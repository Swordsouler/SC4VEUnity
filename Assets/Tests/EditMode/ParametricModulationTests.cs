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
    // Chantier PAR (modulation paramétrique de l'action) : désérialisation du nouveau champ
    // « angle », valeurs par défaut, et détection rule-based de la grandeur discrète
    // (« de 90 degrés ») et des modulateurs graduables (« un peu », « beaucoup »).
    public class ParametricModulationTests
    {
        private RuleBasedIntentRecognizer _recognizer;
        private Language _previousLanguage;

        [SetUp]
        public void SetUp()
        {
            // Les phrases de test sont en français : la locale doit l'être aussi (repli statique
            // de UserData hors Play mode). Restaurée dans TearDown pour ne pas polluer les
            // autres classes de tests.
            _previousLanguage = UserData.Language;
            UserData.Language = Language.French;

            // CommandVocabulary met les triggers en cache PAR LOCALE : si une autre classe de
            // tests a laissé un vocabulaire anglais, « tourne »/« agrandis » ne déclencheraient
            // plus. Application.streamingAssetsPath n'est lisible que sur le thread principal :
            // on le met en cache avant l'attente bloquante (cf. EditModeSync).
            Sven.Utils.SvenSettings.CacheMainThreadPaths();
            EditModeSync.RunSync(() => CommandVocabulary.InitializeAsync());

            // Vocabulaire minimal construit à la main : le recognizer n'a pas besoin de scène.
            // Les triggers viennent de CommandVocabulary (repli sur les attributs C# si
            // l'ontologie n'a pas été chargée dans cette session de tests).
            _recognizer = new RuleBasedIntentRecognizer(
                annotationTypes: new List<string> { "Pomme", "Banane" },
                availableColors: new List<string> { "Rouge", "Vert" },
                pointerDeictics: new List<string> { "ce", "cette", "ça", "ceci" },
                pointerName: "Pointeur",
                cameraName: "Caméra");
        }

        [TearDown]
        public void TearDown() => UserData.Language = _previousLanguage;

        // ─────────────────────────────────────────────────────────────────────
        // Désérialisation JSON (contrat LLM)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Deserialize_RotateLeftWithAngleString_ParsesAngle()
        {
            // « angle » est une chaîne dans le contrat LLM (comme « limit ») : coercition attendue.
            const string json = @"[{""type"": ""RotateLeftCommand"", ""angle"": ""90"", ""parameters"": []}]";
            List<Command> commands = JsonConvert.DeserializeObject<List<Command>>(json);
            RotateLeftCommand rotate = commands[0] as RotateLeftCommand;
            Assert.IsNotNull(rotate);
            Assert.AreEqual(90f, rotate.Angle);
        }

        [Test]
        public void Deserialize_RotateLeftWithoutAngle_Defaultsto45()
        {
            const string json = @"[{""type"": ""RotateLeftCommand"", ""parameters"": []}]";
            List<Command> commands = JsonConvert.DeserializeObject<List<Command>>(json);
            RotateLeftCommand rotate = commands[0] as RotateLeftCommand;
            Assert.IsNotNull(rotate);
            Assert.AreEqual(45f, rotate.Angle, "Sans champ « angle », le comportement historique (45°) doit être conservé.");
        }

        [Test]
        public void Deserialize_RotateRightWithNumericAngle_ParsesAngle()
        {
            const string json = @"[{""type"": ""RotateRightCommand"", ""angle"": 22.5, ""parameters"": []}]";
            List<Command> commands = JsonConvert.DeserializeObject<List<Command>>(json);
            RotateRightCommand rotate = commands[0] as RotateRightCommand;
            Assert.IsNotNull(rotate);
            Assert.AreEqual(22.5f, rotate.Angle);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Mode règles — grandeur discrète (PAR 1)
        // ─────────────────────────────────────────────────────────────────────

        private T RecognizeSingle<T>(string phrase) where T : Command
        {
            string json = _recognizer.Recognize(new Sentence(phrase));
            Assert.IsNotNull(json, $"Aucune commande reconnue pour « {phrase} ».");
            List<Command> commands = JsonConvert.DeserializeObject<List<Command>>(json);
            Assert.AreEqual(1, commands.Count);
            Assert.IsInstanceOf<T>(commands[0], $"Type inattendu pour « {phrase} » : {commands[0].Type}");
            return (T)commands[0];
        }

        [Test]
        public void RuleBased_ExplicitAngle_SetsAngleAndKeepsLimitUnbounded()
        {
            RotateRightCommand cmd = RecognizeSingle<RotateRightCommand>("tourne la pomme de 90 degrés");
            Assert.AreEqual(90f, cmd.Angle);

            // Piège DetectLimit : « 90 » est l'angle, pas une limite de sélection —
            // sinon la commande sélectionnerait 90 objets.
            SelectionParameter selection = cmd.Parameters.OfType<SelectionParameter>().First();
            Assert.AreEqual(-1, selection.Limit, "Le nombre de l'angle ne doit pas devenir une limite de sélection.");
            Assert.IsTrue(selection.Filters.Any(f => !f.IsOperator && f.Condition.Type == "Annotation" && f.Condition.Value == "Pomme"));
        }

        [Test]
        public void RuleBased_DegreeSymbol_SetsAngle()
        {
            RotateRightCommand cmd = RecognizeSingle<RotateRightCommand>("tourne la pomme de 30°");
            Assert.AreEqual(30f, cmd.Angle);
        }

        [Test]
        public void RuleBased_ExplicitAngle_LeftCommandKeepsDirection()
        {
            RotateLeftCommand cmd = RecognizeSingle<RotateLeftCommand>("tourne à gauche la pomme de 90 degrés");
            Assert.AreEqual(90f, cmd.Angle);
        }

        [Test]
        public void RuleBased_QuarterTurn_Is90Degrees()
        {
            RotateRightCommand cmd = RecognizeSingle<RotateRightCommand>("tourne la pomme d'un quart de tour");
            Assert.AreEqual(90f, cmd.Angle);
        }

        [Test]
        public void RuleBased_HalfTurn_Is180Degrees()
        {
            RotateRightCommand cmd = RecognizeSingle<RotateRightCommand>("tourne la pomme d'un demi-tour");
            Assert.AreEqual(180f, cmd.Angle);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Mode règles — modulateurs graduables (PAR 2)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void RuleBased_ALittle_HalvesDefaultAngle()
        {
            RotateRightCommand cmd = RecognizeSingle<RotateRightCommand>("tourne la pomme un peu");
            Assert.AreEqual(22.5f, cmd.Angle, "« un peu » doit donner la moitié de l'incrément par défaut (45°).");
        }

        [Test]
        public void RuleBased_ALot_DoublesDefaultAngle()
        {
            RotateRightCommand cmd = RecognizeSingle<RotateRightCommand>("tourne beaucoup la pomme");
            Assert.AreEqual(90f, cmd.Angle);
        }

        [Test]
        public void RuleBased_ExplicitAngleWinsOverModifier()
        {
            RotateRightCommand cmd = RecognizeSingle<RotateRightCommand>("tourne la pomme un peu de 90 degrés");
            Assert.AreEqual(90f, cmd.Angle, "La grandeur discrète explicite prime sur le modulateur graduable.");
        }

        [Test]
        public void RuleBased_ScaleUpALittle_ModulatesDeviationFromIdentity()
        {
            ScaleUpCommand cmd = RecognizeSingle<ScaleUpCommand>("agrandis la pomme un peu");
            // Le coefficient s'applique à l'écart à ×1 : 1 + 0.1 × 0.5 — pas au facteur brut
            // (1.1 × 0.5 = 0.55 serait une réduction).
            Assert.AreEqual(1.05f, cmd.Factor, 1e-4f);
        }

        [Test]
        public void RuleBased_ScaleUpALot_IsStrongerThanDefault()
        {
            ScaleUpCommand cmd = RecognizeSingle<ScaleUpCommand>("agrandis beaucoup la pomme");
            Assert.AreEqual(1.2f, cmd.Factor, 1e-4f);
            Assert.Greater(cmd.Factor, 1.1f, "« beaucoup » doit être plus marqué que l'incrément par défaut.");
        }

        [Test]
        public void RuleBased_Double_KeepsExplicitFactorOverModifier()
        {
            ScaleUpCommand cmd = RecognizeSingle<ScaleUpCommand>("double la pomme un peu");
            Assert.AreEqual(2f, cmd.Factor, "« double » (grandeur discrète) prime sur « un peu ».");
        }

        [Test]
        public void RuleBased_ScaleDownALot_ModulatesDivisor()
        {
            ScaleDownCommand cmd = RecognizeSingle<ScaleDownCommand>("réduis beaucoup la pomme");
            Assert.AreEqual(1.2f, cmd.Factor, 1e-4f);
        }

        [Test]
        public void RuleBased_ALittle_DoesNotBecomeSelectionLimit()
        {
            // « un » de « un peu » ne doit pas être interprété comme « 1 objet ».
            ScaleUpCommand cmd = RecognizeSingle<ScaleUpCommand>("agrandis les pommes un peu");
            SelectionParameter selection = cmd.Parameters.OfType<SelectionParameter>().First();
            Assert.AreEqual(-1, selection.Limit, "« un peu » ne doit pas limiter la sélection à 1 objet.");
            Assert.AreEqual(1.05f, cmd.Factor, 1e-4f);
        }

        [Test]
        public void RuleBased_PlainRotate_KeepsDefaultAngle()
        {
            RotateRightCommand cmd = RecognizeSingle<RotateRightCommand>("tourne la pomme");
            Assert.AreEqual(45f, cmd.Angle, "Sans grandeur ni modulateur, l'incrément historique est conservé.");
        }
    }
}
