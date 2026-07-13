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
        {
            // Destination ajoutée seulement si la phrase l'indique (« …ici/là-bas/… »).
            // Sans destination (« déplace cette pomme »), le PointParameter manque → clarification.
            // Cible absente (« déplace ici » alors qu'un objet est sélectionné) → sélection courante.
            var ps = new List<Parameter> { ctx.BuildSelectionParameter(useStartedAt: true, fallbackToSelection: true) };
            if (ctx.HasDestination) ps.Add(ctx.BuildDestinationParameter());
            return ps;
        }

        private PointParameter PointParameter => GetParameter<PointParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();

            Vector3? destination = PointParameter?.Point;

            // Fallback : la position n'est pas encore dans le graphe RDF
            // (timestamp trop récent ou graphe non encore peuplé).
            // On lit directement PointerHitPosition depuis la scène.
            if (destination == null && PointParameter != null)
            {
                Pointer pointer = UnityEngine.Object.FindAnyObjectByType<Pointer>();
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

            if (destination == null) return objects;

            return ExecuteReversible(objects, semantizationCore =>
            {
                Transform t = semantizationCore.transform;
                Vector3 prev = t.position;
                Vector3 next = (Vector3)destination;
                t.position = next;
                Debug.Log($"[MoveCommand] Objet {semantizationCore.GetUUID()} déplacé vers {destination}");
                return (() => t.position = prev,
                        () => t.position = next);
            });
        }
    }
}