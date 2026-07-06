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
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, obj =>
            {
                if (!obj.TryGetComponent(out Renderer renderer) || renderer.material == null) return null;

                var prevColor = renderer.material.color;
                var mat       = renderer.material;
                var captured  = obj;

                SetTransparentCommand.SetOpaque(mat);
                var nextColor = mat.color;

                Debug.Log($"[SetOpaque] {obj.GetUUID()} → opaque.");
                return (() => {
                    if (captured.TryGetComponent(out Renderer r))
                        SetTransparentCommand.SetTransparent(r.material, prevColor.a);
                }, () => {
                    if (captured.TryGetComponent(out Renderer r))
                        SetTransparentCommand.SetOpaque(r.material);
                });
            });
        }
    }
}
