using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("pose au sol", "met au sol", "mets au sol", "aligne au sol", "snap au sol", "sol")]
    [Serializable, CommandDescription("Pose les objets sur le sol (raycast vers le bas). Paramètres: SelectionParameter.")]
    public class SnapToGroundCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            var undoActions = new List<Action>();
            var redoActions = new List<Action>();

            foreach (SemantizationCore obj in objects)
            {
                var prevPos  = obj.transform.position;
                var captured = obj;

                // Raycast depuis légèrement au-dessus de l'objet vers le bas
                Vector3 origin = obj.transform.position + Vector3.up * 0.1f;
                Vector3 newPos;
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 100f))
                    newPos = hit.point;
                else
                {
                    newPos   = obj.transform.position;
                    newPos.y = 0f;
                }

                obj.transform.position = newPos;
                var nextPos = newPos;

                undoActions.Add(() => captured.transform.position = prevPos);
                redoActions.Add(() => captured.transform.position = nextPos);
                Debug.Log($"[SnapToGround] {obj.GetUUID()} → {newPos}");
            }

            if (undoActions.Count > 0)
                CommandHistory.Push(
                    () => undoActions.ForEach(a => a()),
                    () => redoActions.ForEach(a => a()));

            return objects;
        }
    }
}
