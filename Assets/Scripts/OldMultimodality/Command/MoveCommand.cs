using System;
using System.Threading.Tasks;

namespace Sven.Command
{
    public class MoveCommand : Command<CommandSettings, PositionParameter>, IBaseCommand
    {
        public DateTime CompletionTime { get; set; }

        public async Task Execute()
        {
            await Task.Yield();
            throw new System.NotImplementedException();
        }
    }
}