using Sven.Content;
using Sven.Multimodality;
using System.Threading.Tasks;
using UnityEngine;

namespace Sven.Command
{
    public class ShowCommand : Command<CommandSettings>, IBaseCommand
    {
        public async Task Execute()
        {
            await Task.Yield();
            foreach (SemantizationCore semantizationCore in MultimodalityController.SelectedObjects)
                if (semantizationCore.TryGetComponent(out Renderer renderer))
                    renderer.enabled = true;
        }
    }
}