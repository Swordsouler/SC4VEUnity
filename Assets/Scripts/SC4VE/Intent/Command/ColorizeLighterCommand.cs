using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("rends plus clair", "rend plus clair", "éclaircis", "éclaircit",
                       "clarifies", "illumine", "éclaire")]
    [Serializable, CommandDescription("Éclaircit la couleur (*2). Paramètres: SelectionParameter.")]
    public class ColorizeLighterCommand : Command
    {
        public override List<SemantizationCore> Execute()
        {
            return ExecuteReversible(SelectionParameter?.Objects ?? new(), obj =>
            {
                if (!obj.TryGetComponent(out Renderer renderer) || renderer.material == null) return null;

                UnityEngine.Color prev = renderer.material.color;
                UnityEngine.Color lighter = new(
                    Mathf.Clamp01(prev.r * 2f),
                    Mathf.Clamp01(prev.g * 2f),
                    Mathf.Clamp01(prev.b * 2f),
                    prev.a);

                renderer.material.color = lighter;
                return (() => renderer.material.color = prev,
                        () => renderer.material.color = lighter);
            });
        }
    }
}