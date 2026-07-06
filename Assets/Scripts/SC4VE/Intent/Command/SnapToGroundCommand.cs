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
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, obj =>
            {
                var prevPos  = obj.transform.position;

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

                Debug.Log($"[SnapToGround] {obj.GetUUID()} → {newPos}");
                return (() => obj.transform.position = prevPos,
                        () => obj.transform.position = nextPos);
            });
        }
    }
}
