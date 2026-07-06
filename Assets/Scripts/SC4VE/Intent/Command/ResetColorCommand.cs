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
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, obj =>
            {
                if (!obj.TryGetComponent(out Renderer renderer) || renderer.material == null) return null;

                var prevColor = renderer.material.color;
                var captured  = obj;

                OriginalStateStore.RestoreColor(obj);

                var nextColor = renderer.material.color;

                Debug.Log($"[ResetColor] {obj.GetUUID()} → couleur d'origine.");
                return (() => {
                    if (captured.TryGetComponent(out Renderer r))
                        r.material.color = prevColor;
                }, () => OriginalStateStore.RestoreColor(captured));
            });
        }
    }
}
