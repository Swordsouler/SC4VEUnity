using Sven.Content;
using System.Collections.Generic;
using UnityEngine;

namespace Sven.Command
{
    public class ColorizeCommand : ActionCommand<Color>, IBaseCommand<object>
    {
        public object Execute(IReadOnlyList<SemantizationCore> semantizationCores)
        {
            foreach (SemantizationCore semantizationCore in semantizationCores)
                if (semantizationCore.TryGetComponent(out Renderer renderer))
                    renderer.material.color = Parameter;
            return null;
        }
    }
}