using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable]
    public class ScaleDownCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            foreach (SemantizationCore semantizationCore in objects)
            {
                semantizationCore.transform.localScale *= 0.9f;
                Debug.Log($"Scaling down object {semantizationCore.GetUUID()} to scale {semantizationCore.transform.localScale}");
            }
            return objects;
        }
    }
}