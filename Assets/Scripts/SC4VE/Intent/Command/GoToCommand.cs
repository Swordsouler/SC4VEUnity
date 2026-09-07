using Sven.Content;
using Sven.Context;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers(
        "va là", "va là-bas", "va ici", "va à", "rejoins", "rejoindre",
        "go there", "go here", "go to")]
    [Serializable, CommandDescription(
        "Envoie un serveur à un endroit désigné par pointage (« toi, va là-bas »). " +
        "Paramètres: SelectionParameter (le serveur) + PointParameter (la destination). " +
        "Ne déplace QUE des serveurs : pour déplacer un objet, générer MoveCommand.")]
    public class GoToCommand : Command
    {
        /// <summary>
        /// Calque de MoveCommand : même forme de paramètres, seul Execute change. Au lieu
        /// d'écrire transform.position, il pose une destination sur le NavMeshAgent — le
        /// serveur y marche au lieu d'y être téléporté, ce qui est tout l'intérêt de déléguer.
        /// </summary>
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
        {
            // Repli sur la sélection courante : « va là-bas » juste après avoir pointé un
            // serveur ne demande pas de le redésigner.
            var ps = new List<Parameter> { ctx.BuildSelectionParameter(fallbackToSelection: true) };
            if (ctx.HasDestination) ps.Add(ctx.BuildDestinationParameter());
            return ps;
        }

        private PointParameter PointParameter => GetParameter<PointParameter>();

        public override List<SemantizationCore> Execute()
        {
            Waiter agent = DelegationRoles.Agent(DelegationRoles.AllTargets(this));

            if (agent == null)
            {
                Speak(UserData.Locale == "fr" ? "Quel serveur ?" : "Which waiter?");
                return new();
            }

            Vector3? destination = PointParameter?.Point;

            // Même repli que MoveCommand : la position pointée peut n'être pas encore dans le
            // graphe (horodatage trop récent). On lit alors le pointeur directement.
            if (destination == null && PointParameter != null)
            {
                Pointer pointer = UnityEngine.Object.FindAnyObjectByType<Pointer>();
                if (pointer != null)
                {
                    destination = pointer.PointerHitPosition;
                    Debug.LogWarning("[GoTo] Position absente du graphe — repli sur " +
                                     $"Pointer.PointerHitPosition : {destination}");
                }
                else Debug.LogError("[GoTo] Aucun Pointer dans la scène.");
            }

            if (destination == null) return new();

            agent.GoTo((Vector3)destination);
            return new List<SemantizationCore> { agent.GetComponent<SemantizationCore>() };
        }
    }
}
