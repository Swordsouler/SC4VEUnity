using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("taille normale", "taille originale", "taille par défaut", "réinitialise la taille", "reset la taille")]
    [Serializable, CommandDescription("Remet la taille des objets à (1,1,1). Paramètres: SelectionParameter.")]
    public class ResetScaleCommand : Command
    {
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, obj =>
            {
                var prev = obj.transform.localScale;
                obj.transform.localScale = Vector3.one;
                Debug.Log($"[ResetScale] {obj.GetUUID()} → (1,1,1)");
                return (() => obj.transform.localScale = prev,
                        () => obj.transform.localScale = Vector3.one);
            });
        }
    }
}
