using Sven.Content;
using Sven.Multimodality;
using System.Collections.Generic;

namespace Sven.Command
{
    public class SelectCommand : FilterCommand
    {
        public override void Apply(List<SemantizationCore> semantizationCores)
        {
            MultimodalityController.AddSelectedObjects(semantizationCores);
        }
    }
}