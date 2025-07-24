using Sven.Content;
using Sven.Multimodality;
using System.Threading.Tasks;
using UnityEngine;

namespace Sven.Command
{
    public class ColorizeCommand : Command<ColorizeSettings, ColorParameter>, IBaseCommand
    {
        public async Task Execute()
        {
            foreach (SemantizationCore semantizationCore in MultimodalityController.SelectedObjects)
                if (semantizationCore.TryGetComponent(out Renderer renderer))
                    renderer.material.color = Parameter.MaxColor;
        }
    }
}