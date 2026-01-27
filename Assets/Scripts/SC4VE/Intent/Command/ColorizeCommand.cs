using Newtonsoft.Json;
using Sven.Content;
using System;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable]
    public class ColorizeCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();
        private ColorParameter ColorParameter => GetParameter<ColorParameter>();

        public override void Execute()
        {
            foreach (SemantizationCore semantizationCore in SelectionParameter.Objects)
            {
                if (!semantizationCore.TryGetComponent(out Renderer renderer) || renderer.material == null) continue;
                Debug.Log(JsonConvert.SerializeObject(ColorParameter.Color));
                renderer.material.color = ColorParameter.Color.Value;
                Debug.Log($"Colorizing object {semantizationCore.GetUUID()} with color {ColorParameter.Color.Value}");
            }
        }
    }
}