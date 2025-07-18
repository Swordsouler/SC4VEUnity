using Sven.Multimodality;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sven.Command
{
    public class CommandChain
    {
        private List<IBaseCommand<object>> _commands = new();
        public IReadOnlyList<IBaseCommand<object>> Commands => _commands;

        public CommandChain(Sentence sentence)
        {
            _commands = new();
        }

        public void AddCommand(IBaseCommand<object> command)
        {
            _commands.Add(command);
        }

        public void AddCommands(IEnumerable<IBaseCommand<object>> commands)
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

        public void RemoveCommand(IBaseCommand<object> command)
        {
            _commands.Remove(command);
        }

        public void RemoveCommands(IEnumerable<IBaseCommand<object>> commands)
        {
            foreach (var command in commands)
            {
                _commands.Remove(command);
            }
        }

        public async Task Execute(MultimodalityController multimodalityController)
        {
            await Task.Yield();
            FilterCommand filterCommand = null;
            List<QueryFilter<BaseCommandSettings>> filters = new();
            foreach (var command in _commands)
            {
                switch (command)
                {
                    case QueryFilter<BaseCommandSettings> queryFilter:
                        MultimodalityController.AddSelectedObjects(await queryFilter.Execute(), true);
                        await ApplyFilters(filterCommand, filters);
                        break;

                    case FilterCommand filter:
                        filterCommand = filter;
                        await ApplyFilters(filterCommand, filters);
                        break;

                    default:
                        command.Execute(MultimodalityController.SelectedObjects);
                        break;
                }
            }
        }

        public async Task ApplyFilters(FilterCommand filterCommand, List<QueryFilter<BaseCommandSettings>> filters)
        {
            if (filterCommand == null) return;
            if (filters == null || filters.Count == 0) return;

            foreach (QueryFilter<BaseCommandSettings> filter in filters)
                MultimodalityController.AddSelectedObjects(await filter.Execute(), true);
        }
    }
}