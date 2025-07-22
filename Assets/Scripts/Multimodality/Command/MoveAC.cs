using Sven.Content;
using System.Collections.Generic;

namespace Sven.Command
{
    public class MoveAC : ActionCommand<CommandSettings, PositionParameter>, IBaseCommand<object>
    {
        public object Execute(IReadOnlyList<SemantizationCore> semantizationCores)
        {
            throw new System.NotImplementedException();
        }
    }
}