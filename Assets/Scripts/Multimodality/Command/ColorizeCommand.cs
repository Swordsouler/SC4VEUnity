using Sven.Content;
using Sven.Multimodality;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Sven.Command
{
    public class ColorizeCommand : Command<ColorizeSettings, ColorParameter>, IBaseCommand
    {
        public DateTime CompletionTime { get; set; }

        public async Task Execute()
        {
            await Task.Yield();
            foreach (SemantizationCore semantizationCore in MultimodalityController.SelectedObjects)
                if (semantizationCore.TryGetComponent(out Renderer renderer) && renderer.material != null)
                    renderer.material.color = Parameter.MaxColor;
        }
    }
}