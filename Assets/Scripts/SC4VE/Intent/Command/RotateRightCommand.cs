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
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, obj =>
            {
                var prev = obj.transform.rotation;
                obj.transform.Rotate(Vector3.up, 45f, Space.World);
                var next = obj.transform.rotation;
                return (() => obj.transform.rotation = prev,
                        () => obj.transform.rotation = next);
            });
        }
    }
}
