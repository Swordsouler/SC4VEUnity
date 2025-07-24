using System.Threading.Tasks;

namespace Sven.Command
{
    public class MoveCommand : Command<CommandSettings, PositionParameter>, IBaseCommand
    {
        public async Task Execute()
        {
            throw new System.NotImplementedException();
        }
    }
}