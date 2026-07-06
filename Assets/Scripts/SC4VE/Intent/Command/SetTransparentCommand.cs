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
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, obj =>
            {
                if (!obj.TryGetComponent(out Renderer renderer) || renderer.material == null) return null;

                var  prevColor = renderer.material.color;
                var  prevMode  = renderer.material.HasProperty("_Mode")
                    ? renderer.material.GetFloat("_Mode") : -1f;
                var mat      = renderer.material;
                var captured = obj;

                SetTransparent(mat, TargetAlpha);
                var nextColor = mat.color;

                Debug.Log($"[SetTransparent] {obj.GetUUID()} alpha → {TargetAlpha}");
                return (() => {
                    if (prevMode >= 0) SetOpaque(captured.TryGetComponent(out Renderer r) ? r.material : mat);
                    if (captured.TryGetComponent(out Renderer rr)) rr.material.color = prevColor;
                }, () => {
                    if (captured.TryGetComponent(out Renderer r)) SetTransparent(r.material, TargetAlpha);
                });
            });
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
            UnityEngine.Color c = mat.color;
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
            UnityEngine.Color c = mat.color;
            c.a = 1f;
            mat.color = c;
        }
    }
}
