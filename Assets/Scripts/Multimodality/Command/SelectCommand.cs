using Sven.Multimodality;
using System.Threading.Tasks;

namespace Sven.Command
{
    public class SelectCommand : FilterCommand
    {
        public override async Task Execute()
        {
            if (Parameter == null) return;

            var result = await Task.Run(async () =>
            {
                var result = await Parameter.Query();
                return result;
            });
            MultimodalityController.AddSelectedObjects(result, false);
        }
    }
}