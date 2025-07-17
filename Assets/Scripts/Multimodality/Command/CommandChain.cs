using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sven.Command
{
    public class CommandChain
    {
        private List<BaseCommand<BaseCommandSettings>> _commands = new();
        public IReadOnlyList<BaseCommand<BaseCommandSettings>> Commands => _commands;

        public void AddCommand(BaseCommand<BaseCommandSettings> command)
        {
            _commands.Add(command);
        }

        public void AddCommands(IEnumerable<BaseCommand<BaseCommandSettings>> commands)
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

        public void RemoveCommand(BaseCommand<BaseCommandSettings> command)
        {
            _commands.Remove(command);
        }

        public void RemoveCommands(IEnumerable<BaseCommand<BaseCommandSettings>> commands)
        {
            foreach (var command in commands)
            {
                _commands.Remove(command);
            }
        }

        public async Task<List<BaseCommand<BaseCommandSettings>>> Execute()
        {
            await Task.Yield();
            throw new System.NotImplementedException("Execution logic not implemented yet. This method should execute each command in the chain and return the results.");
        }
    }
}