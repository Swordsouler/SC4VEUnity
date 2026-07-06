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
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, obj =>
            {
                var prev = obj.transform.rotation;
                obj.transform.Rotate(Vector3.up, 180f, Space.World);
                var next = obj.transform.rotation;
                return (() => obj.transform.rotation = prev,
                        () => obj.transform.rotation = next);
            });
        }
    }
}
