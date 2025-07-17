using Sven.Content;
using System.Collections.Generic;
using UnityEngine;

namespace Sven.Command
{
    public class MoveCommand : ActionCommand<Vector3>
    {
        public override void Apply(Vector3 value, IReadOnlyList<SemantizationCore> semantizationCores)
        {
            //foreach (SemantizationCore semantizationCore in semantizationCores)
            //    if (semantizationCore.TryGetComponent(out Renderer renderer))
            //        renderer.material.color = value;
        }
    }
}