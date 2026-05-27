using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("réinitialise", "réinitialiser", "remet en place", "position d'origine",
                       "état d'origine", "reset", "restaure la position")]
    [Serializable, CommandDescription("Remet les objets à leur position/rotation/taille d'origine. Paramètres: SelectionParameter.")]
    public class ResetTransformCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            var undoActions = new List<Action>();
            var redoActions = new List<Action>();

            foreach (SemantizationCore obj in objects)
            {
                var prevPos   = obj.transform.position;
                var prevRot   = obj.transform.rotation;
                var prevScale = obj.transform.localScale;
                var captured  = obj;

                OriginalStateStore.RestoreTransform(obj);

                var newPos   = obj.transform.position;
                var newRot   = obj.transform.rotation;
                var newScale = obj.transform.localScale;

                undoActions.Add(() => {
                    captured.transform.position   = prevPos;
                    captured.transform.rotation   = prevRot;
                    captured.transform.localScale = prevScale;
                });
                redoActions.Add(() => {
                    captured.transform.position   = newPos;
                    captured.transform.rotation   = newRot;
                    captured.transform.localScale = newScale;
                });
                Debug.Log($"[ResetTransform] {obj.GetUUID()} → position d'origine.");
            }

            if (undoActions.Count > 0)
                CommandHistory.Push(
                    () => undoActions.ForEach(a => a()),
                    () => redoActions.ForEach(a => a()));

            return objects;
        }
    }
}
