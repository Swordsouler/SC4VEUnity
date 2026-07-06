using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription("Change la taille (réduction). Paramètres: SelectionParameter.")]
    [RuleBasedTriggers("diminue la taille", "scale down", "rapetisse", "rapetissit", "réduis",
                       "réduit", "diminue", "rétrécis", "rétrécit", "rapetisser", "réduire", "rétrécir")]
    public class ScaleDownCommand : Command
    {
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, semantizationCore =>
            {
                Transform t = semantizationCore.transform;
                Vector3 prev = t.localScale;
                Vector3 next = prev / 1.1f;
                t.localScale = next;
                Debug.Log($"Scaling down object {semantizationCore.GetUUID()} to {t.localScale}");
                return (() => t.localScale = prev,
                        () => t.localScale = next);
            });
        }
    }
}