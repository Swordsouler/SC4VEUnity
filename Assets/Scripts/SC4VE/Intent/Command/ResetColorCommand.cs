using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("couleur d'origine", "couleur originale", "couleur par défaut",
                       "réinitialise la couleur", "reset la couleur", "restaure la couleur")]
    [Serializable, CommandDescription("Remet les objets à leur couleur d'origine. Paramètres: SelectionParameter.")]
    public class ResetColorCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            var undoActions = new List<Action>();
            var redoActions = new List<Action>();

            foreach (SemantizationCore obj in objects)
            {
                if (!obj.TryGetComponent(out Renderer renderer) || renderer.material == null) continue;

                var prevColor = renderer.material.color;
                var captured  = obj;

                OriginalStateStore.RestoreColor(obj);

                var nextColor = renderer.material.color;

                undoActions.Add(() => {
                    if (captured.TryGetComponent(out Renderer r))
                        r.material.color = prevColor;
                });
                redoActions.Add(() => OriginalStateStore.RestoreColor(captured));
                Debug.Log($"[ResetColor] {obj.GetUUID()} → couleur d'origine.");
            }

            if (undoActions.Count > 0)
                CommandHistory.Push(
                    () => undoActions.ForEach(a => a()),
                    () => redoActions.ForEach(a => a()));

            return objects;
        }
    }
}
