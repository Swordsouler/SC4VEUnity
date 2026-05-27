using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("retourne", "retourner", "inverse", "inverser", "miroir", "flip")]
    [Serializable, CommandDescription("Retourne les objets de 180° (axe Y). Paramètres: SelectionParameter.")]
    public class FlipCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            var undoActions = new List<Action>();
            var redoActions = new List<Action>();

            foreach (SemantizationCore obj in objects)
            {
                var prev = obj.transform.rotation;
                obj.transform.Rotate(Vector3.up, 180f, Space.World);
                var next = obj.transform.rotation;
                var captured = obj;
                undoActions.Add(() => captured.transform.rotation = prev);
                redoActions.Add(() => captured.transform.rotation = next);
            }

            if (undoActions.Count > 0)
                CommandHistory.Push(
                    () => undoActions.ForEach(a => a()),
                    () => redoActions.ForEach(a => a()));

            return objects;
        }
    }
}
