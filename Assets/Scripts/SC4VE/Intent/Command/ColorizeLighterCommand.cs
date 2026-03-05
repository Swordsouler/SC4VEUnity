using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription("Éclaircit la couleur (*2). Paramètres: SelectionParameter.")]
    public class ColorizeLighterCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            foreach (SemantizationCore semantizationCore in objects)
            {
                if (!semantizationCore.TryGetComponent(out Renderer renderer) || renderer.material == null) continue;

                UnityEngine.Color current = renderer.material.color;
                UnityEngine.Color lighter = new(
                    Mathf.Clamp01(current.r * 2f),
                    Mathf.Clamp01(current.g * 2f),
                    Mathf.Clamp01(current.b * 2f),
                    current.a);

                renderer.material.color = lighter;
            }

            return objects;
        }
    }
}