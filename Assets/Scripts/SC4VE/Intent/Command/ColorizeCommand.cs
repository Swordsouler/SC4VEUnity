using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription("Applique une couleur. Paramètres: ColorParameter, SelectionParameter.")]
    public class ColorizeCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();
        private ColorParameter ColorParameter => GetParameter<ColorParameter>();

        public override List<SemantizationCore> Execute()
        {
            if (SelectionParameter == null) Debug.Log($"Executing ColorizeCommand with color {ColorParameter.Color.Value} on {SelectionParameter.Objects.Count} objects.");
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