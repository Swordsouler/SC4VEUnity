using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("tourne à droite", "pivote à droite", "rotation droite", "tourne droite", "tourne", "pivote", "faire pivoter", "rotation")]
    [Serializable, CommandDescription("Fait pivoter les objets de 45° vers la droite (axe Y). Paramètres: SelectionParameter.")]
    public class RotateRightCommand : Command
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
                obj.transform.Rotate(Vector3.up, 45f, Space.World);
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
