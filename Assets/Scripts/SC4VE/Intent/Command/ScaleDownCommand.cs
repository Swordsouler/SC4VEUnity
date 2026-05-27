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
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            foreach (SemantizationCore semantizationCore in objects)
            {
                semantizationCore.transform.localScale /= 1.1f;
                Debug.Log($"Scaling down object {semantizationCore.GetUUID()} to {semantizationCore.transform.localScale}");
            }
            return objects;
        }
    }
}