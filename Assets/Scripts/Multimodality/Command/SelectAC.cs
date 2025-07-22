using Sven.Content;
using Sven.Multimodality;
using System.Collections.Generic;

namespace Sven.Command
{
    public class SelectAC : FilterAC
    {
        public override object Execute(IReadOnlyList<SemantizationCore> semantizationCores)
        {
            MultimodalityController.AddSelectedObjects(semantizationCores, true);
            return null;
        }
    }
}