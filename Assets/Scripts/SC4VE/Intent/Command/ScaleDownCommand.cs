using Sven.Content;
using System;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable]
    public class ScaleDownCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override void Execute()
        {
            foreach (SemantizationCore semantizationCore in SelectionParameter.Objects)
            {
                semantizationCore.transform.localScale *= 0.9f;
                Debug.Log($"Scaling down object {semantizationCore.GetUUID()} to scale {semantizationCore.transform.localScale}");
            }
        }
    }
}