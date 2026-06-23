using Sven.Content;
using Sven.Context;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers(
        "met ici", "mets ici", "met là", "mets là", "met là-bas", "mets là-bas",
        "met là-haut", "mets là-haut",
        "déplace", "déplacer", "bouge", "bouger", "amène", "amener", "place", "placer",
        "move", "repositionne", "repositionner", "transporte", "transporter")]
    [Serializable, CommandDescription("Déplace des objets. Paramètres: SelectionParameter (source), et soit PointParameter (destination) soit SelectionParameter (destination).")]
    public class MoveCommand : Command
    {
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
            => new List<Parameter> { ctx.BuildSelectionParameter(useStartedAt: true), ctx.BuildDestinationParameter() };

        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();
        private PointParameter PointParameter => GetParameter<PointParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;

            Vector3? destination = PointParameter?.Point;

            // Fallback : la position n'est pas encore dans le graphe RDF
            // (timestamp trop récent ou graphe non encore peuplé).
            // On lit directement PointerHitPosition depuis la scène.
            if (destination == null && PointParameter != null)
            {
                Pointer pointer = UnityEngine.Object.FindFirstObjectByType<Pointer>();
                if (pointer != null)
                {
                    destination = pointer.PointerHitPosition;
                    Debug.LogWarning(
                        $"[MoveCommand] Position introuvable dans le graphe — " +
                        $"fallback sur Pointer.PointerHitPosition : {destination}");
                }
                else
                {
                    Debug.LogError("[MoveCommand] Aucun Pointer trouvé dans la scène.");
                }
            }

            foreach (SemantizationCore semantizationCore in objects)
            {
                if (destination == null) continue;
                semantizationCore.transform.position = (Vector3)destination;
                Debug.Log($"[MoveCommand] Objet {semantizationCore.GetUUID()} déplacé vers {destination}");
            }
            return objects;
        }
    }
}