using Sven.Content;
using Sven.Multimodality;
using System;
using System.Threading.Tasks;

namespace Sven.Command
{
    public class ScaleDownCommand : Command<CommandSettings>, IBaseCommand
    {
        public DateTime CompletionTime { get; set; }

        public async Task Execute()
        {
            await Task.Yield();
            foreach (SemantizationCore semantizationCore in MultimodalityController.SelectedObjects)
                semantizationCore.transform.localScale /= 1.25f;
        }
    }
}