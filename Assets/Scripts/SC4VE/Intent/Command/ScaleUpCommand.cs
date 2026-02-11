using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable]
    public class ScaleUpCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            foreach (SemantizationCore semantizationCore in objects)
            {
                semantizationCore.transform.localScale *= 1.1f;
                Debug.Log($"Scaling up object {semantizationCore.GetUUID()} to scale {semantizationCore.transform.localScale}");
            }
            return objects;
        }
    }
}