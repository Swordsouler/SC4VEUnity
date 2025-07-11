using Sven.Content;
using System.Collections.Generic;
using UnityEngine;

namespace Sven.Command
{
    public class MoveCommand : ActionCommand<Vector3>
    {
        public override void Apply(Vector3 value, List<SemantizationCore> semantizationCores)
        {
            throw new System.NotImplementedException($"Apply value '{value}' to {semantizationCores}");
        }
    }
}