using Sven.Content;
using Sven.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription("Duplique les objets sélectionnés. Si un PointParameter (destination) est fourni, la copie est instanciée à cet endroit (ex: « copie cet objet ici ») ; sinon juste au-dessus de l'original. Paramètres: SelectionParameter, PointParameter (optionnel, destination).")]
    [RuleBasedTriggers("duplique", "dupliquer", "clone", "cloner", "crée une copie", "créer une copie", "copie", "copier")]
    public class DuplicateCommand : Command
    {
        // Comme MoveCommand : sélection source pointée avant de parler, + destination
        // optionnelle (uniquement si la phrase indique un endroit, « copie cet objet ici »).
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
        {
            // Cible absente (« fais une copie ici » alors qu'un objet est sélectionné) → sélection courante.
            var ps = new List<Parameter> { ctx.BuildSelectionParameter(useStartedAt: true, fallbackToSelection: true) };
            if (ctx.HasDestination) ps.Add(ctx.BuildDestinationParameter());
            return ps;
        }

        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();
        private PointParameter PointParameter => GetParameter<PointParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            Vector3? destination = ResolveDestination();

            List<SemantizationCore> duplicates = new();
            foreach (SemantizationCore semantizationCore in objects)
            {
                GameObject duplicatedGameObject = UnityEngine.Object.Instantiate(semantizationCore.gameObject);
                // Avec destination : on instancie au pointeur. Sinon : 1 unité au-dessus de l'original.
                duplicatedGameObject.transform.position = destination
                    ?? semantizationCore.transform.position + Vector3.up;

                if (duplicatedGameObject.TryGetComponent(out SemantizationCore duplicate))
                    duplicates.Add(duplicate);

                Debug.Log($"[Duplicate] Copie de {semantizationCore.GetUUID()} instanciée à {duplicatedGameObject.transform.position}.");
            }

            // Retourne les copies (et non les originaux) pour la coréférence : « copie ça ici » puis « agrandis-le ».
            return duplicates.Count > 0 ? duplicates : objects;
        }

        /// <summary>
        /// Résout la destination de pointage si un PointParameter est présent.
        /// Reprend le fallback de MoveCommand : si la position n'est pas encore dans
        /// le graphe RDF, on lit directement Pointer.PointerHitPosition.
        /// </summary>
        private Vector3? ResolveDestination()
        {
            if (PointParameter == null) return null;

            Vector3? destination = PointParameter.Point;
            if (destination == null)
            {
                Pointer pointer = UnityEngine.Object.FindFirstObjectByType<Pointer>();
                if (pointer != null)
                {
                    destination = pointer.PointerHitPosition;
                    Debug.LogWarning(
                        "[Duplicate] Position introuvable dans le graphe — " +
                        $"fallback sur Pointer.PointerHitPosition : {destination}");
                }
                else
                {
                    Debug.LogError("[Duplicate] Aucun Pointer trouvé dans la scène.");
                }
            }
            return destination;
        }
    }
}