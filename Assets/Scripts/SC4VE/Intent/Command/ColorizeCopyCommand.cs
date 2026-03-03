using Sven.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable]
    public class ColorizeCopyCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();
        private SelectionParameter SelectionParameter2 => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            SemantizationCore firstObject = SelectionParameter2.Objects.FirstOrDefault();
            foreach (SemantizationCore semantizationCore in objects)
            {
                if (!semantizationCore.TryGetComponent(out Renderer renderer) || renderer.material == null) continue;
                if (!firstObject.TryGetComponent(out Renderer renderer2) || renderer2.material == null) continue;
                renderer.material.color = renderer2.material.color;
            }
            return objects;
        }
    }
}