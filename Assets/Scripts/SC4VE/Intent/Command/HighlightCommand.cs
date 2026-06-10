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

        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            var undoActions = new List<Action>();
            var redoActions = new List<Action>();

            foreach (SemantizationCore obj in objects)
            {
                if (!obj.TryGetComponent(out Renderer renderer) || renderer.material == null) continue;
                if (!renderer.material.HasProperty("_EmissionColor")) continue;

                string uuid     = obj.GetUUID();
                bool wasOn      = _highlighted.Contains(uuid);
                var  captured   = obj;
                var  capturedId = uuid;

                if (wasOn)
                    RemoveHighlight(renderer, capturedId);
                else
                    AddHighlight(renderer, capturedId);

                undoActions.Add(() => {
                    if (!captured.TryGetComponent(out Renderer r)) return;
                    if (wasOn) AddHighlight(r, capturedId); else RemoveHighlight(r, capturedId);
                });
                redoActions.Add(() => {
                    if (!captured.TryGetComponent(out Renderer r)) return;
                    if (wasOn) RemoveHighlight(r, capturedId); else AddHighlight(r, capturedId);
                });
            }

            if (undoActions.Count > 0)
                CommandHistory.Push(
                    () => undoActions.ForEach(a => a()),
                    () => redoActions.ForEach(a => a()));

            return objects;
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
