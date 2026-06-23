using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription("Masque les objets. Paramètres: SelectionParameter.")]
    [RuleBasedTriggers("rend invisible", "rends invisible", "masque", "cache", "dissimule",
                       "masquer", "cacher", "dissumuler", "invisibilise", "invisibiliser")]
    public class HideCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            var undoActions = new List<Action>();
            var redoActions = new List<Action>();
            foreach (SemantizationCore semantizationCore in objects)
            {
                if (!semantizationCore.TryGetComponent(out Renderer renderer)) continue;
                Renderer captured = renderer;
                bool prev = renderer.enabled;
                renderer.enabled = false;
                undoActions.Add(() => { if (captured != null) captured.enabled = prev; });
                redoActions.Add(() => { if (captured != null) captured.enabled = false; });
            }
            if (undoActions.Count > 0)
                CommandHistory.Push(
                    () => undoActions.ForEach(a => a()),
                    () => redoActions.ForEach(a => a()));
            return objects;
        }
    }
}