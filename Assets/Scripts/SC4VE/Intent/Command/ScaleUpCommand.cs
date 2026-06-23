using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription("Change la taille (agrandissement). Paramètres: SelectionParameter.")]
    [RuleBasedTriggers("augmente la taille", "scale up", "grossis", "grossit", "agrandis",
                       "agrandit", "grandit", "grandir", "grossir", "agrandir")]
    public class ScaleUpCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            var undoActions = new List<Action>();
            var redoActions = new List<Action>();
            foreach (SemantizationCore semantizationCore in objects)
            {
                Transform t = semantizationCore.transform;
                Vector3 prev = t.localScale;
                Vector3 next = prev * 1.1f;
                t.localScale = next;
                undoActions.Add(() => { if (t != null) t.localScale = prev; });
                redoActions.Add(() => { if (t != null) t.localScale = next; });
                Debug.Log($"Scaling up object {semantizationCore.GetUUID()} to {t.localScale}");
            }
            if (undoActions.Count > 0)
                CommandHistory.Push(
                    () => undoActions.ForEach(a => a()),
                    () => redoActions.ForEach(a => a()));
            return objects;
        }
    }
}