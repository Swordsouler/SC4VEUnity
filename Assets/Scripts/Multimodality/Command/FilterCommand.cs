using Sven.Content;
using System.Collections.Generic;

namespace Sven.Command
{
    public abstract class FilterCommand : BaseCommand<CommandSettings>
    {
        public abstract void Apply(List<SemantizationCore> semantizationCores);
    }
}