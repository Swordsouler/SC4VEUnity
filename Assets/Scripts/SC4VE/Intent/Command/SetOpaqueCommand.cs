using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("rends opaque", "rend opaque", "opaque", "rends visible complètement",
                       "enlève la transparence", "retire la transparence")]
    [Serializable, CommandDescription("Rend les objets entièrement opaques (alpha 100%). Paramètres: SelectionParameter.")]
    public class SetOpaqueCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            var undoActions = new List<Action>();
            var redoActions = new List<Action>();

            foreach (SemantizationCore obj in objects)
            {
                if (!obj.TryGetComponent(out Renderer renderer) || renderer.material == null) continue;

                var prevColor = renderer.material.color;
                var mat       = renderer.material;
                var captured  = obj;

                SetTransparentCommand.SetOpaque(mat);
                var nextColor = mat.color;

                undoActions.Add(() => {
                    if (captured.TryGetComponent(out Renderer r))
                        SetTransparentCommand.SetTransparent(r.material, prevColor.a);
                });
                redoActions.Add(() => {
                    if (captured.TryGetComponent(out Renderer r))
                        SetTransparentCommand.SetOpaque(r.material);
                });
                Debug.Log($"[SetOpaque] {obj.GetUUID()} → opaque.");
            }

            if (undoActions.Count > 0)
                CommandHistory.Push(
                    () => undoActions.ForEach(a => a()),
                    () => redoActions.ForEach(a => a()));

            return objects;
        }
    }
}
