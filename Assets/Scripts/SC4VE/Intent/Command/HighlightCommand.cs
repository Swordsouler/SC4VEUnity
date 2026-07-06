using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("surligne", "souligne", "met en évidence", "mets en évidence",
                       "illumine", "illuminer", "highlight")]
    [Serializable, CommandDescription("Active/désactive la mise en évidence (émission) des objets. Paramètres: SelectionParameter.")]
    public class HighlightCommand : Command
    {
        private static readonly HashSet<string> _highlighted = new();
        private static readonly UnityEngine.Color HighlightColor = UnityEngine.Color.yellow * 0.5f;

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, obj =>
            {
                if (!obj.TryGetComponent(out Renderer renderer) || renderer.material == null) return null;
                if (!renderer.material.HasProperty("_EmissionColor")) return null;

                string uuid     = obj.GetUUID();
                bool wasOn      = _highlighted.Contains(uuid);
                var  captured   = obj;
                var  capturedId = uuid;

                if (wasOn)
                    RemoveHighlight(renderer, capturedId);
                else
                    AddHighlight(renderer, capturedId);

                return (() => {
                    if (!captured.TryGetComponent(out Renderer r)) return;
                    if (wasOn) AddHighlight(r, capturedId); else RemoveHighlight(r, capturedId);
                }, () => {
                    if (!captured.TryGetComponent(out Renderer r)) return;
                    if (wasOn) RemoveHighlight(r, capturedId); else AddHighlight(r, capturedId);
                });
            });
        }

        private static void AddHighlight(Renderer renderer, string uuid)
        {
            renderer.material.EnableKeyword("_EMISSION");
            renderer.material.SetColor("_EmissionColor", HighlightColor);
            _highlighted.Add(uuid);
            Debug.Log($"[Highlight] {uuid} → ON");
        }

        private static void RemoveHighlight(Renderer renderer, string uuid)
        {
            renderer.material.DisableKeyword("_EMISSION");
            renderer.material.SetColor("_EmissionColor", UnityEngine.Color.black);
            _highlighted.Remove(uuid);
            Debug.Log($"[Highlight] {uuid} → OFF");
        }
    }
}
