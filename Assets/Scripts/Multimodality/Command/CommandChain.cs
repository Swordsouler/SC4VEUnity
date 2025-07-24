using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sven.Command
{
    public class CommandChain
    {
        private List<IBaseCommand> _commands = new();
        public IReadOnlyList<IBaseCommand> Commands => _commands;

        public CommandChain()
        {
            _commands = new();
        }

        public CommandChain(Sentence sentence) : this()
        {

        }

        public void AddCommand(IBaseCommand command)
        {
            _commands.Add(command);
        }

        public void AddCommands(IEnumerable<IBaseCommand> commands)
        {
            foreach (var command in commands)
            {
                _commands.Add(command);
            }
        }

        public void ClearCommands()
        {
            _commands.Clear();
        }

        public void RemoveCommand(IBaseCommand command)
        {
            _commands.Remove(command);
        }

        public void RemoveCommands(IEnumerable<IBaseCommand> commands)
        {
            foreach (var command in commands)
            {
                _commands.Remove(command);
            }
        }

        public async Task Execute()
        {
            await Task.Yield();
            foreach (var command in _commands)
            {
                await command.Execute();
            }
            return;
            /*await Task.Yield();
            FilterAC filterCommand = null;
            List<QueryFilter<BaseSettings>> filters = new();
            foreach (var command in _commands)
            {
                switch (command)
                {
                    case QueryFilter<BaseSettings> queryFilter:
                        MultimodalityController.AddSelectedObjects(await queryFilter.Execute(), true);
                        await ApplyFilters(filterCommand, filters);
                        break;

                    case FilterAC filter:
                        filterCommand = filter;
                        await ApplyFilters(filterCommand, filters);
                        break;

                    case IBaseCommand baseCommand:
                        baseCommand.Execute();
                        break;
                }
            }*/
        }

        /*public async Task ApplyFilters(FilterAC filterCommand, List<QueryFilter<BaseSettings>> filters)
        {
            if (filterCommand == null) return;
            if (filters == null || filters.Count == 0) return;

            foreach (QueryFilter<BaseSettings> filter in filters)
                MultimodalityController.AddSelectedObjects(await filter.Execute(), true);
        }*/
    }
}