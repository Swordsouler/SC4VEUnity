using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("aligne", "aligner", "même hauteur", "à la même hauteur")]
    [Serializable, CommandDescription("Aligne les objets à la même hauteur (Y) que le premier sélectionné. Paramètres: SelectionParameter.")]
    public class AlignCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            if (objects.Count < 2)
            {
                Debug.Log("[Align] Deux objets minimum requis pour l'alignement.");
                return objects;
            }

            float targetY   = objects[0].transform.position.y;
            var undoActions = new List<Action>();
            var redoActions = new List<Action>();

            foreach (SemantizationCore obj in objects)
            {
                var prevPos  = obj.transform.position;
                var newPos   = new Vector3(obj.transform.position.x, targetY, obj.transform.position.z);
                var captured = obj;

                obj.transform.position = newPos;

                undoActions.Add(() => captured.transform.position = prevPos);
                redoActions.Add(() => captured.transform.position = newPos);
                Debug.Log($"[Align] {obj.GetUUID()} → Y={targetY}");
            }

            CommandHistory.Push(
                () => undoActions.ForEach(a => a()),
                () => redoActions.ForEach(a => a()));

            return objects;
        }
    }
}
