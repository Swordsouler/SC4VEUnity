namespace Sven.Command
{
    public abstract class FilterCommand : Command<CommandSettings, IQueryFilter>, IBaseCommand
    {
        public abstract void Execute();
    }
}