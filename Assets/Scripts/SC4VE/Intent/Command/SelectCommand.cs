using Sven.Content;
using System;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable]
    public class SelectCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override void Execute()
        {
            foreach (SemantizationCore semantizationCore in SelectionParameter.Objects)
            {
                throw new NotImplementedException("Select functionality is not implemented yet.");
            }
        }
    }
}