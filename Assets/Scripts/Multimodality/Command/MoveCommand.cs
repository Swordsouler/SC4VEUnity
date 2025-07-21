using Sven.Content;
using System.Collections.Generic;

namespace Sven.Command
{
    public class MoveCommand : ActionCommand<PositionParameter>, IBaseCommand<object>
    {
        public object Execute(IReadOnlyList<SemantizationCore> semantizationCores)
        {
            throw new System.NotImplementedException();
        }
    }
}