using Sven.Content;
using Sven.Multimodality;
using System.Collections.Generic;

namespace Sven.Command
{
    public class UnselectAC : FilterAC
    {
        public override object Execute(IReadOnlyList<SemantizationCore> semantizationCores)
        {
            MultimodalityController.RemoveSelectedObjects(semantizationCores);
            return null;
        }
    }
}