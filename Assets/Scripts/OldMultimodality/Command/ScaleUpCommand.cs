using Sven.Content;
using Sven.Multimodality;
using System;
using System.Threading.Tasks;

namespace Sven.Command
{
    public class ScaleUpCommand : Command<CommandSettings>, IBaseCommand
    {
        public DateTime CompletionTime { get; set; }

        public async Task Execute()
        {
            await Task.Yield();
            MultimodalityController.EnqueueMainThreadAction(() =>
            {
                foreach (SemantizationCore semantizationCore in MultimodalityController.SelectedObjects)
                    semantizationCore.transform.localScale *= 1.25f;
            });
        }
    }
}