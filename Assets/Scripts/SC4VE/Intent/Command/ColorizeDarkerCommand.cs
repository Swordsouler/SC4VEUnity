using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("rends plus sombre", "rend plus sombre", "assombris", "assombrit",
                       "noircis", "noircit", "fonce", "foncer", "obscurcis", "obscurcit")]
    [Serializable, CommandDescription("Assombrit la couleur (/2). Paramètres: SelectionParameter.")]
    public class ColorizeDarkerCommand : Command
    {
        public override List<SemantizationCore> Execute()
        {
            return ExecuteReversible(SelectionParameter?.Objects ?? new(), obj =>
            {
                if (!obj.TryGetComponent(out Renderer renderer) || renderer.material == null) return null;

                UnityEngine.Color prev = renderer.material.color;
                UnityEngine.Color darker = new(
                    Mathf.Clamp01(prev.r / 2f),
                    Mathf.Clamp01(prev.g / 2f),
                    Mathf.Clamp01(prev.b / 2f),
                    prev.a);

                renderer.material.color = darker;
                return (() => renderer.material.color = prev,
                        () => renderer.material.color = darker);
            });
        }
    }
}