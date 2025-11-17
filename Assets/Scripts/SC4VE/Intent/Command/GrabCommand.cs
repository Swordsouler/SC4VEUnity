using Sven.Content;
using System;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable]
    public class GrabCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override void Execute()
        {
            foreach (SemantizationCore semantizationCore in SelectionParameter.Objects)
            {
                if (!semantizationCore.TryGetComponent(out Renderer renderer)) continue;
                throw new NotImplementedException("Grab functionality is not implemented yet.");
            }
        }
    }
}