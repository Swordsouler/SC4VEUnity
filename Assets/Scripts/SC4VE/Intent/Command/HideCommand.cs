using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription("Masque les objets. Paramètres: SelectionParameter.")]
    [RuleBasedTriggers("rend invisible", "rends invisible", "masque", "cache", "dissimule",
                       "masquer", "cacher", "dissumuler", "invisibilise", "invisibiliser")]
    public class HideCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            foreach (SemantizationCore semantizationCore in objects)
            {
                if (!semantizationCore.TryGetComponent(out Renderer renderer)) continue;
                renderer.enabled = false;
            }
            return objects;
        }
    }
}