using Sven.Content;
using System.Collections.Generic;

namespace Sven.Command
{
    public abstract class FilterAC : ActionCommand<CommandSettings, IReadOnlyList<SemantizationCore>>, IBaseCommand<object>
    {
        public abstract object Execute(IReadOnlyList<SemantizationCore> semantizationCores);
    }
}