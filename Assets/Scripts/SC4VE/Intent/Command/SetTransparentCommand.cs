using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("rends transparent", "rend transparent", "transparence", "semi-transparent",
                       "rends semi-transparent", "rend semi-transparent")]
    [Serializable, CommandDescription("Rend les objets semi-transparents (alpha 30%). Paramètres: SelectionParameter.")]
    public class SetTransparentCommand : Command
    {
        private const float TargetAlpha = 0.3f;
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            var undoActions = new List<Action>();
            var redoActions = new List<Action>();

            foreach (SemantizationCore obj in objects)
            {
                if (!obj.TryGetComponent(out Renderer renderer) || renderer.material == null) continue;

                var  prevColor = renderer.material.color;
                var  prevMode  = renderer.material.HasProperty("_Mode")
                    ? renderer.material.GetFloat("_Mode") : -1f;
                var mat      = renderer.material;
                var captured = obj;

                SetTransparent(mat, TargetAlpha);
                var nextColor = mat.color;

                undoActions.Add(() => {
                    if (prevMode >= 0) SetOpaque(captured.TryGetComponent(out Renderer r) ? r.material : mat);
                    if (captured.TryGetComponent(out Renderer rr)) rr.material.color = prevColor;
                });
                redoActions.Add(() => {
                    if (captured.TryGetComponent(out Renderer r)) SetTransparent(r.material, TargetAlpha);
                });
                Debug.Log($"[SetTransparent] {obj.GetUUID()} alpha → {TargetAlpha}");
            }

            if (undoActions.Count > 0)
                CommandHistory.Push(
                    () => undoActions.ForEach(a => a()),
                    () => redoActions.ForEach(a => a()));

            return objects;
        }

        internal static void SetTransparent(Material mat, float alpha)
        {
            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
            Color c = mat.color;
            c.a = alpha;
            mat.color = c;
        }

        internal static void SetOpaque(Material mat)
        {
            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 0);
                mat.SetInt("_SrcBlend", (int)BlendMode.One);
                mat.SetInt("_DstBlend", (int)BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = -1;
            }
            Color c = mat.color;
            c.a = 1f;
            mat.color = c;
        }
    }
}
