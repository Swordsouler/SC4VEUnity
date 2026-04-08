using System;
using System.Threading.Tasks;

namespace Sven.Command
{
    public class RepeatCommand : Command<CommandSettings>, IBaseCommand
    {
        public DateTime CompletionTime { get; set; }

        public async Task Execute()
        {
            await Task.Yield();
            CommandChain.Repeat();
        }
    }
}