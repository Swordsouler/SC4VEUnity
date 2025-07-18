using Sven.Content;
using Sven.Multimodality;
using System.Collections.Generic;

namespace Sven.Command
{
    public class UnselectCommand : FilterCommand
    {
        public override object Execute(IReadOnlyList<SemantizationCore> semantizationCores)
        {
            MultimodalityController.RemoveSelectedObjects(semantizationCores);
            return null;
        }
    }
}