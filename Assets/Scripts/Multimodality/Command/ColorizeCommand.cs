using Sven.Content;
using System.Collections.Generic;
using UnityEngine;

namespace Sven.Command
{
    public class ColorizeCommand : ActionCommand<Color>
    {
        public override void Apply(Color value, List<SemantizationCore> semantizationCores)
        {
            throw new System.NotImplementedException($"Apply value '{value}' to {semantizationCores}");
        }
    }
}