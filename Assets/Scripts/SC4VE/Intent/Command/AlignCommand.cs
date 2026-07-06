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
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            if (objects.Count < 2)
            {
                Debug.Log("[Align] Deux objets minimum requis pour l'alignement.");
                return objects;
            }

            float targetY = objects[0].transform.position.y;
            return ExecuteReversible(objects, obj =>
            {
                Vector3 prevPos = obj.transform.position;
                Vector3 newPos  = new(obj.transform.position.x, targetY, obj.transform.position.z);
                obj.transform.position = newPos;
                Debug.Log($"[Align] {obj.GetUUID()} → Y={targetY}");
                return (() => obj.transform.position = prevPos,
                        () => obj.transform.position = newPos);
            });
        }
    }
}
