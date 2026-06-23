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
            ps.Add(ctx.BuildSelectionParameter());
            return ps;
        }

        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();
        private ColorParameter ColorParameter => GetParameter<ColorParameter>();

        public override List<SemantizationCore> Execute()
        {
            if (SelectionParameter == null)
            {
                Debug.LogWarning("ColorizeCommand: no SelectionParameter, nothing to colorize.");
                return new();
            }
            List<SemantizationCore> objects = SelectionParameter.Objects;
            Debug.Log($"Executing ColorizeCommand on {objects.Count} object(s).");
            UnityEngine.Color target = ColorParameter.Color.Value;
            var undoActions = new List<Action>();
            var redoActions = new List<Action>();
            foreach (SemantizationCore semantizationCore in objects)
            {
                if (!semantizationCore.TryGetComponent(out Renderer renderer) || renderer.material == null) continue;
                Renderer captured = renderer;
                UnityEngine.Color prev = renderer.material.color;
                renderer.material.color = target;
                undoActions.Add(() => { if (captured != null && captured.material != null) captured.material.color = prev; });
                redoActions.Add(() => { if (captured != null && captured.material != null) captured.material.color = target; });
                Debug.Log($"Colorizing object {semantizationCore.GetUUID()} with color {target}");
            }
            if (undoActions.Count > 0)
                CommandHistory.Push(
                    () => undoActions.ForEach(a => a()),
                    () => redoActions.ForEach(a => a()));
            return objects;
        }
    }
}