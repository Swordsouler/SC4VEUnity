using Sven.Multimodality;
using System;
using System.Threading.Tasks;

namespace Sven.Command
{
    public class SelectCommand : Command<CommandSettings, IQueryFilter>, IBaseCommand
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
            MultimodalityController.AddSelectedObjects(result, true);
        }
    }
}