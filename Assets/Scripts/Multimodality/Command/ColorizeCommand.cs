using Sven.Content;
using System.Collections.Generic;
using UnityEngine;

namespace Sven.Command
{
    public class ColorizeCommand : ActionCommand<Color>
    {
        public override void Apply(Color value, IReadOnlyList<SemantizationCore> semantizationCores)
        {
            foreach (SemantizationCore semantizationCore in semantizationCores)
                if (semantizationCore.TryGetComponent(out Renderer renderer))
                    renderer.material.color = value;
        }
    }
}