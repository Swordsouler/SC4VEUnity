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
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, semantizationCore =>
            {
                if (!semantizationCore.TryGetComponent(out Renderer renderer)) return null;
                Renderer captured = renderer;
                bool prev = renderer.enabled;
                renderer.enabled = false;
                return (() => { if (captured != null) captured.enabled = prev; },
                        () => { if (captured != null) captured.enabled = false; });
            });
        }
    }
}