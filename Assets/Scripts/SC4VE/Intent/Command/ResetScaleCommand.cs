using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("taille normale", "taille originale", "taille par défaut", "réinitialise la taille", "reset la taille")]
    [Serializable, CommandDescription("Remet la taille des objets à (1,1,1). Paramètres: SelectionParameter.")]
    public class ResetScaleCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            var undoActions = new List<Action>();
            var redoActions = new List<Action>();

            foreach (SemantizationCore obj in objects)
            {
                var prev = obj.transform.localScale;
                obj.transform.localScale = Vector3.one;
                var captured = obj;
                undoActions.Add(() => captured.transform.localScale = prev);
                redoActions.Add(() => captured.transform.localScale = Vector3.one);
                Debug.Log($"[ResetScale] {obj.GetUUID()} → (1,1,1)");
            }

            if (undoActions.Count > 0)
                CommandHistory.Push(
                    () => undoActions.ForEach(a => a()),
                    () => redoActions.ForEach(a => a()));

            return objects;
        }
    }
}
