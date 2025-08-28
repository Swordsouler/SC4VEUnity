using System;
using System.Threading.Tasks;

namespace Sven.Command
{
    public class EventCommand : Command<EventSettings>, IBaseCommand
    {
        public DateTime CompletionTime { get; set; }

        public async Task Execute()
        {
            await Task.Yield();
        }
    }
}