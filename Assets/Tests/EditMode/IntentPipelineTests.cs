using NUnit.Framework;
using Sc4ve.Multimodality;
using System.Linq;

namespace Sc4ve.Tests.EditMode
{
    // Le trajet d'une phrase que montre la tablette de la main gauche : seules les étapes
    // sollicitées, et les phrases suggérées après deux phrases non comprises de suite.
    public class IntentPipelineTests
    {
        [SetUp]
        public void SetUp() => IntentPipeline.Reset();

        [TearDown]
        public void TearDown() => IntentPipeline.Reset();

        private static IntentPipeline.Stage[] Stages()
            => IntentPipeline.Steps.Select(s => s.Stage).ToArray();

        [Test]
        public void EtapeJamaisSollicitee_NApparaitPas()
        {
            // Mode hybride, règles sûres d'elles, commande sans objet à chercher (« prépare
            // une salade ») : ni LLM, ni recherche sémantique.
            IntentPipeline.Begin();
            IntentPipeline.Start(IntentPipeline.Stage.Rules);
            IntentPipeline.End(IntentPipeline.Stage.Rules, IntentPipeline.Status.Done);
            IntentPipeline.Complete(null, "executed", 1);

            CollectionAssert.AreEqual(
                new[] { IntentPipeline.Stage.Rules, IntentPipeline.Stage.Execution }, Stages());
            Assert.AreEqual(IntentPipeline.Status.Done, IntentPipeline.Steps[1].Status);
        }

        [Test]
        public void RelaisHybride_LeLlmEnEchec_LesReglesSontRetenues()
        {
            IntentPipeline.Begin();
            IntentPipeline.Start(IntentPipeline.Stage.Rules);
            IntentPipeline.End(IntentPipeline.Stage.Rules, IntentPipeline.Status.Relayed,
                               IntentPipeline.Reason.MissedTarget);
            IntentPipeline.Start(IntentPipeline.Stage.Llm);
            IntentPipeline.End(IntentPipeline.Stage.Llm, IntentPipeline.Status.Failed, IntentPipeline.Reason.Timeout);
            IntentPipeline.Revise(IntentPipeline.Stage.Rules, IntentPipeline.Status.Done, IntentPipeline.Reason.Kept);

            IntentPipeline.Step rules = IntentPipeline.Steps[0];
            IntentPipeline.Step llm = IntentPipeline.Steps[1];
            Assert.AreEqual(IntentPipeline.Status.Done, rules.Status);
            Assert.AreEqual(IntentPipeline.Reason.Kept, rules.Reason);
            Assert.AreEqual(IntentPipeline.Status.Failed, llm.Status);
            Assert.AreEqual(IntentPipeline.Reason.Timeout, llm.Reason);
        }

        [Test]
        public void EndSurUneEtapeDejaClose_NeChangeRien()
        {
            // Le contrôleur clôt les règles « au cas où » après plusieurs chemins : un verdict
            // déjà rendu ne doit pas être écrasé.
            IntentPipeline.Begin();
            IntentPipeline.Start(IntentPipeline.Stage.Rules);
            IntentPipeline.End(IntentPipeline.Stage.Rules, IntentPipeline.Status.Failed,
                               IntentPipeline.Reason.NothingRecognized);
            IntentPipeline.End(IntentPipeline.Stage.Rules, IntentPipeline.Status.Done);

            Assert.AreEqual(IntentPipeline.Status.Failed, IntentPipeline.Steps[0].Status);
        }

        [Test]
        public void DeuxPhrasesNonComprisesDeSuite_DemandentDeLAide()
        {
            IntentPipeline.Begin();
            IntentPipeline.Complete(null, "not_understood", 0);
            Assert.IsFalse(IntentPipeline.HelpWanted, "Une seule phrase non comprise ne suffit pas.");

            IntentPipeline.Begin();
            IntentPipeline.Complete(null, "not_understood", 0);
            Assert.IsTrue(IntentPipeline.HelpWanted);

            // Une erreur interne n'est pas la faute du joueur : l'aide reste.
            IntentPipeline.Begin();
            IntentPipeline.Complete(null, "error", 0);
            Assert.IsTrue(IntentPipeline.HelpWanted);

            // Une phrase comprise — même par une question en retour — la retire.
            IntentPipeline.Begin();
            IntentPipeline.Complete(null, "clarification", 0);
            Assert.IsFalse(IntentPipeline.HelpWanted);
        }

        [Test]
        public void Reinitialisation_OublieLesPhrasesNonComprises()
        {
            // Entre deux visiteurs (ResetSceneCommand, au menu pause comme à la voix), le
            // suivant ne doit pas hériter des phrases suggérées de celui d'avant.
            IntentPipeline.Begin();
            IntentPipeline.Complete(null, "not_understood", 0);
            IntentPipeline.Begin();
            IntentPipeline.Complete(null, "not_understood", 0);
            Assert.IsTrue(IntentPipeline.HelpWanted);

            IntentPipeline.ForgetMisunderstandings();
            Assert.IsFalse(IntentPipeline.HelpWanted);
            Assert.AreEqual(0, IntentPipeline.Misunderstood);
        }

        [Test]
        public void UneSeuleIssueParPhrase()
        {
            IntentPipeline.Begin();
            IntentPipeline.Complete(null, "not_understood", 0);
            IntentPipeline.Complete(null, "not_understood", 0);
            Assert.AreEqual(1, IntentPipeline.Misunderstood);
        }

        [Test]
        public void ExecuteeSansEffetEtCommentee_EstUnRefus()
        {
            // « Fais une salade de fruits » sans regarder le cuisinier : rien de fait, et la
            // voix du système dit pourquoi.
            IntentPipeline.Begin();
            IntentPipeline.NoteSpoken("Il faut me regarder pour me demander un plat.");
            IntentPipeline.Complete(null, "executed", 0);

            Assert.AreEqual(IntentPipeline.Status.Refused, IntentPipeline.Steps.Last().Status);
            Assert.AreEqual("Il faut me regarder pour me demander un plat.", IntentPipeline.Spoken);
        }

        [Test]
        public void QuestionEtDesambiguisation_SontDesQuestions()
        {
            IntentPipeline.Begin();
            IntentPipeline.Complete(null, "clarification", 0);
            Assert.AreEqual(IntentPipeline.Status.Question, IntentPipeline.Steps.Last().Status);

            IntentPipeline.Begin();
            IntentPipeline.Complete(null, "disambiguation", 2);
            Assert.AreEqual(IntentPipeline.Status.Question, IntentPipeline.Steps.Last().Status);
            Assert.AreEqual(IntentPipeline.Reason.WhichOne, IntentPipeline.Steps.Last().Reason);
        }

        [Test]
        public void AucunObjet_EchoueALaRechercheEtNExecuteRien()
        {
            IntentPipeline.Begin();
            IntentPipeline.Start(IntentPipeline.Stage.Rules);
            IntentPipeline.End(IntentPipeline.Stage.Rules, IntentPipeline.Status.Done);
            IntentPipeline.Start(IntentPipeline.Stage.Semantic);
            IntentPipeline.End(IntentPipeline.Stage.Semantic, IntentPipeline.Status.Done);
            IntentPipeline.Complete(null, "no_match", 0);

            CollectionAssert.AreEqual(
                new[] { IntentPipeline.Stage.Rules, IntentPipeline.Stage.Semantic }, Stages());
            Assert.AreEqual(IntentPipeline.Status.Failed, IntentPipeline.Steps[1].Status);
            Assert.AreEqual(IntentPipeline.Reason.NoObject, IntentPipeline.Steps[1].Reason);
        }

        [Test]
        public void NouvellePhrase_EffaceLeTrajetPrecedent()
        {
            IntentPipeline.Begin();
            IntentPipeline.Start(IntentPipeline.Stage.Rules);
            IntentPipeline.NoteSpoken("Quelle table ?");
            IntentPipeline.Complete(null, "clarification", 0);

            IntentPipeline.Begin();
            Assert.AreEqual(0, IntentPipeline.Steps.Count);
            Assert.IsNull(IntentPipeline.Outcome);
            Assert.IsNull(IntentPipeline.Spoken);
        }
    }
}
