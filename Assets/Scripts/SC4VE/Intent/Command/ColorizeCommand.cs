using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable]
    public class ColorizeCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();
        private ColorParameter ColorParameter => GetParameter<ColorParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            foreach (SemantizationCore semantizationCore in objects)
            {
                if (!semantizationCore.TryGetComponent(out Renderer renderer) || renderer.material == null) continue;
                renderer.material.color = ColorParameter.Color.Value;
                Debug.Log($"Colorizing object {semantizationCore.GetUUID()} with color {ColorParameter.Color.Value}");
            }
            return objects;
        }
    }
}