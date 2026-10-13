using Sc4ve.Multimodality.Intent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality
{
    /// <summary>
    /// Le trajet de la DERNIÈRE phrase dans le système — règles, LLM, recherche sémantique,
    /// exécution —, que la tablette de la main gauche affiche étape par étape.
    ///
    /// N'y figurent que les étapes SOLLICITÉES : en hybride, le LLM n'apparaît que si les
    /// règles lui passent la main ; la recherche sémantique, que si une commande désigne des
    /// objets ; l'exécution, que si une commande y est arrivée. Une étape jamais sollicitée
    /// n'est pas « en attente » : elle n'existe pas pour cette phrase.
    ///
    /// Un témoin, pas un acteur : MultimodalityController pose les étapes à mesure qu'il les
    /// traverse, MultimodalityMetrics l'issue — le point par lequel passe déjà chaque phrase —
    /// et Command.Speak ce que la voix du système a répondu. Rien dans le jeu n'en dépend.
    /// </summary>
    public static class IntentPipeline
    {
        public enum Stage { Rules, Llm, Semantic, Execution }

        public enum Status
        {
            Running,
            Done,
            /// <summary>Les règles passent la main au LLM (mode hybride).</summary>
            Relayed,
            Failed,
            /// <summary>Le système pose une question : paramètre ou rôle manquant, « laquelle ? ».</summary>
            Question,
            /// <summary>Exécutée sans rien faire, et la voix du système a dit pourquoi.</summary>
            Refused,
        }

        /// <summary>Le mot du verdict d'une étape ; la tablette le traduit.</summary>
        public enum Reason { None, NothingRecognized, GuessedVerb, MissedTarget, VagueDish, Kept, NotUnderstood, Timeout, NoObject, WhichOne }

        public sealed class Step
        {
            public Stage Stage { get; internal set; }
            public Status Status { get; internal set; }
            public Reason Reason { get; internal set; }
            internal float StartedAt;
            internal float EndedAt;

            /// <summary>Secondes réelles passées dans l'étape, figées à sa clôture.</summary>
            public float Seconds => (Status == Status.Running ? Time.unscaledTime : EndedAt) - StartedAt;
        }

        private static readonly List<Step> _steps = new();

        /// <summary>Les étapes sollicitées par la phrase en cours, dans l'ordre.</summary>
        public static IReadOnlyList<Step> Steps => _steps;

        /// <summary>L'issue de la phrase — celles de MultimodalityMetrics : « executed »,
        /// « clarification », « disambiguation », « no_match », « not_understood »,
        /// « orphan », « error » —, ou null tant qu'elle est en cours.</summary>
        public static string Outcome { get; private set; }

        /// <summary>La commande retenue (la première d'une cascade), ou null.</summary>
        public static Command Command { get; private set; }

        /// <summary>Ce que la voix du système a répondu à cette phrase — question, refus,
        /// « je n'ai pas compris » —, ou null.</summary>
        public static string Spoken { get; private set; }

        /// <summary>
        /// Phrases non comprises D'AFFILÉE. Une phrase comprise — même par une question en
        /// retour — remet le compte à zéro ; une erreur interne n'y touche pas, ce n'est pas
        /// le joueur qui s'est mal fait comprendre.
        /// </summary>
        public static int Misunderstood { get; private set; }

        /// <summary>Deux phrases non comprises de suite : la tablette souffle des phrases.</summary>
        public static bool HelpWanted => Misunderstood >= 2;

        /// <summary>Une nouvelle phrase : le trajet de la précédente s'efface.</summary>
        public static void Begin()
        {
            _steps.Clear();
            Outcome = null;
            Command = null;
            Spoken = null;
        }

        public static void Start(Stage stage)
            => _steps.Add(new Step { Stage = stage, Status = Status.Running, StartedAt = Time.unscaledTime });

        /// <summary>Clôt l'étape si elle est EN COURS ; sans effet sinon — ce qui permet de
        /// la clore « au cas où » après plusieurs chemins.</summary>
        public static void End(Stage stage, Status status, Reason reason = Reason.None)
        {
            Step step = Last(stage);
            if (step == null || step.Status != Status.Running) return;
            Close(step, status, reason);
        }

        /// <summary>Révise le verdict d'une étape déjà close : les règles « retenues » quand
        /// le LLM consulté n'a rien rendu, la recherche « sans objet » quand rien ne correspond.</summary>
        public static void Revise(Stage stage, Status status, Reason reason = Reason.None)
        {
            Step step = Last(stage);
            if (step == null) return;
            if (step.Status == Status.Running) Close(step, status, reason);
            else
            {
                step.Status = status;
                step.Reason = reason;
            }
        }

        /// <summary>Ce que la voix du système vient de dire (Command.Speak, sans locuteur).</summary>
        public static void NoteSpoken(string text)
        {
            if (!string.IsNullOrWhiteSpace(text)) Spoken = text;
        }

        /// <summary>
        /// L'issue de la phrase, une seule fois. L'exécution n'apparaît qu'ici : elle n'existe
        /// que si une commande y est arrivée, et elle est instantanée — la course du serveur
        /// qui suit est le monde qui bouge, pas le traitement de la phrase.
        /// </summary>
        public static void Complete(Command command, string outcome, int affectedCount)
        {
            if (Outcome != null) return;
            Outcome = outcome;
            Command = command;

            // Une étape restée ouverte (exception en route) ne tourne pas indéfiniment.
            foreach (Step step in _steps.Where(s => s.Status == Status.Running).ToList())
                Close(step, Status.Failed, Reason.None);

            switch (outcome)
            {
                case "executed":
                    // Rien de fait ET une réponse du système : un refus (« il faut me
                    // regarder pour me demander un plat »), pas une réussite muette.
                    AddExecution(Spoken != null && affectedCount == 0 ? Status.Refused : Status.Done);
                    break;
                case "clarification":
                    AddExecution(Status.Question);
                    break;
                case "disambiguation":
                    AddExecution(Status.Question, Reason.WhichOne);
                    break;
                case "no_match":
                    Revise(Stage.Semantic, Status.Failed, Reason.NoObject);
                    break;
            }

            if (outcome == "not_understood" || outcome == "orphan") Misunderstood++;
            else if (outcome != "error") Misunderstood = 0;
        }

        /// <summary>
        /// Réinitialisation de la scène (ResetSceneCommand) : le compteur repart de zéro, et le
        /// visiteur suivant n'hérite pas des phrases suggérées de celui d'avant. Le trajet de
        /// la phrase en cours reste : la réinitialisation peut être cette phrase-là.
        /// </summary>
        public static void ForgetMisunderstandings() => Misunderstood = 0;

        /// <summary>Oublie tout, compte compris — utile surtout aux tests.</summary>
        public static void Reset()
        {
            Begin();
            Misunderstood = 0;
        }

        private static void AddExecution(Status status, Reason reason = Reason.None)
        {
            float now = Time.unscaledTime;
            _steps.Add(new Step { Stage = Stage.Execution, Status = status, Reason = reason, StartedAt = now, EndedAt = now });
        }

        private static Step Last(Stage stage) => _steps.LastOrDefault(s => s.Stage == stage);

        private static void Close(Step step, Status status, Reason reason)
        {
            step.Status = status;
            step.Reason = reason;
            step.EndedAt = Time.unscaledTime;
        }
    }
}
