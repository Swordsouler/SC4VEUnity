using Sven.Multimodality;
using System.Threading.Tasks;

namespace Sven.Command
{
    public class UnselectCommand : FilterCommand
    {
        public override void Execute()
        {
            if (Parameter == null) return;

            Task.Run(async () =>
            {
                var result = await Parameter.Execute();
                MultimodalityController.RemoveSelectedObjects(result);
            });
        }
    }
}