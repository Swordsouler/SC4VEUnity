using Sven.Content;
using Sven.Multimodality;
using System.Collections.Generic;

namespace Sven.Command
{
    public class UnselectCommand : FilterCommand
    {
        public override void Apply(List<SemantizationCore> semantizationCores)
        {
            MultimodalityController.RemoveSelectedObjects(semantizationCores);
        }
    }
}