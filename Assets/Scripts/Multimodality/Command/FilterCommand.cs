using Sven.Content;
using System.Collections.Generic;

namespace Sven.Command
{
    public abstract class FilterCommand : Command
    {
        public abstract void Apply(List<SemantizationCore> semantizationCores);
    }
}