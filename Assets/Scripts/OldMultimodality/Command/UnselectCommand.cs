using Sven.Multimodality;
using System;
using System.Threading.Tasks;

namespace Sven.Command
{
    public class UnselectCommand : Command<CommandSettings, IQueryFilter>, IBaseCommand
    {
        public DateTime CompletionTime { get; set; }

        public async Task Execute()
        {
            if (Parameter == null) return;

            var result = await Task.Run(async () =>
            {
                var result = await Parameter.Query();
                return result;
            });
            MultimodalityController.RemoveSelectedObjects(result);
        }
    }
}