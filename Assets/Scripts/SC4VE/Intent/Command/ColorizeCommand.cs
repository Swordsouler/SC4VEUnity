using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers(
        "change la couleur", "met en couleur", "mets en couleur",
        "colorie", "coloris", "colorise", "colorisez", "coloriez",
        "peins", "peinez", "recolore", "recolorez",
        "colorier", "coloriser",
        "met", "mets", "mettre")]
    [Serializable, CommandDescription("Applique une couleur. Paramètres: ColorParameter, SelectionParameter.")]
    public class ColorizeCommand : Command
    {
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
        {
            var ps = new List<Parameter>();
            if (ctx.TargetColors.Count > 0)
                ps.Add(new ColorParameter
                {
                    Type      = "ColorParameter",
                    Value     = ctx.TargetColors[0].Value,
                    Timestamp = ctx.TargetColors[0].Timestamp
                });
            ps.Add(ctx.BuildSelectionParameter(fallbackToSelection: FallbackToSelectionWhenEmpty));
            return ps;
        }

        private ColorParameter ColorParameter => GetParameter<ColorParameter>();

        public override List<SemantizationCore> Execute()
        {
            if (SelectionParameter == null)
            {
                Debug.LogWarning("ColorizeCommand: no SelectionParameter, nothing to colorize.");
                return new();
            }
            // Couleur absente OU hors vocabulaire (le LLM peut halluciner un nom de couleur
            // que QueryColor ne résout pas → Color reste null) : sans cette garde → NullReferenceException.
            Color colorEntry = ColorParameter?.Color;
            if (colorEntry == null)
            {
                Debug.LogWarning("ColorizeCommand: couleur cible absente ou inconnue du vocabulaire — commande ignorée.");
                return new();
            }
            UnityEngine.Color target = colorEntry.Value;

            List<SemantizationCore> objects = SelectionParameter.Objects;
            Debug.Log($"Executing ColorizeCommand on {objects.Count} object(s).");
            return ExecuteReversible(objects, obj =>
            {
                if (!obj.TryGetComponent(out Renderer renderer) || renderer.material == null) return null;
                UnityEngine.Color prev = renderer.material.color;
                renderer.material.color = target;
                Debug.Log($"Colorizing object {obj.GetUUID()} with color {target}");
                return (() => renderer.material.color = prev,
                        () => renderer.material.color = target);
            });
        }
    }
}