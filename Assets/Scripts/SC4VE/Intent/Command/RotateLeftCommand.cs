using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("tourne à gauche", "pivote à gauche", "rotation gauche", "tourne gauche")]
    [Serializable, CommandDescription("Fait pivoter les objets de 45° vers la gauche (axe Y). Paramètres: SelectionParameter.")]
    public class RotateLeftCommand : Command
    {
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, obj =>
            {
                var prev = obj.transform.rotation;
                obj.transform.Rotate(Vector3.up, -45f, Space.World);
                var next = obj.transform.rotation;
                return (() => obj.transform.rotation = prev,
                        () => obj.transform.rotation = next);
            });
        }
    }
}
