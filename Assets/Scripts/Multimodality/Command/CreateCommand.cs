using Sven.Content;
using Sven.Multimodality;
using System;
using System.Threading.Tasks;

namespace Sven.Command
{
    public class CreateCommand : Command<CommandSettings, AnnotationFilter>, IBaseCommand
    {
        public DateTime CompletionTime { get; set; }

        public async Task Execute()
        {
            await Task.Yield();
            foreach (SemantizationCore semantizationCore in MultimodalityController.SelectedObjects)
                semantizationCore.Destroy();
        }
    }
}