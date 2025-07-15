using Sven.Content;
using System.Collections.Generic;

namespace Sven.Command
{
    public abstract class ActionCommand<T> : BaseCommand<CommandSettings>
    {
        public abstract void Apply(T value, List<SemantizationCore> semantizationCores);

        public void Apply(T value)
        {
            List<SemantizationCore> selectedObjects = new();
            Apply(value, selectedObjects);
        }
    }
}