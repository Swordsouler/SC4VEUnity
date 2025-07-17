using Sven.Content;
using Sven.Multimodality;
using System.Collections.Generic;

namespace Sven.Command
{
    public abstract class ActionCommand<T> : BaseCommand<CommandSettings>
    {
        public abstract void Apply(T value, IReadOnlyList<SemantizationCore> semantizationCores);

        public void Apply(T value)
        {
            Apply(value, MultimodalityController.SelectedObjects);
        }
    }
}