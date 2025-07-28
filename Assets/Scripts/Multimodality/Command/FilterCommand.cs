using System;
using System.Threading.Tasks;

namespace Sven.Command
{
    public abstract class FilterCommand : Command<CommandSettings, IQueryFilter>, IBaseCommand
    {
        public DateTime CompletionTime { get; set; }
        public abstract Task Execute();
    }
}