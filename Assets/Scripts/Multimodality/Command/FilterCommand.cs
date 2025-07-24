using System.Threading.Tasks;

namespace Sven.Command
{
    public abstract class FilterCommand : Command<CommandSettings, IQueryFilter>, IBaseCommand
    {
        public abstract Task Execute();
    }
}