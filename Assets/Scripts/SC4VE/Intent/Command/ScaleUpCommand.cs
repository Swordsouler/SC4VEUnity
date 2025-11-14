using Sven.Content;
using System;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable]
    public class ScaleUpCommand : Command
    {
        public SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override void Execute()
        {
            foreach (SemantizationCore semantizationCore in SelectionParameter.Objects)
            {
                semantizationCore.transform.localScale *= 1.1f;
                Debug.Log($"Scaling up object {semantizationCore.GetUUID()} to scale {semantizationCore.transform.localScale}");
            }
        }
    }
}