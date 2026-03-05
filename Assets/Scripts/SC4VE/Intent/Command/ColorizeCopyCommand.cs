using Sven.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription("Applique la couleur d'un objet à un autre. Paramètres: SelectionParameter (cible), SelectionParameter (source).")]
    public class ColorizeCopyCommand : Command
    {
        private SelectionParameter SelectionParameterTarget => GetParameter<SelectionParameter>(1);
        private SelectionParameter SelectionParameterSource => GetParameter<SelectionParameter>(2);

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> targetObjects = SelectionParameterTarget.Objects;
            SemantizationCore sourceObject = SelectionParameterSource.Objects.FirstOrDefault();
            if (!sourceObject.TryGetComponent(out MeshRenderer sourceRenderer) || sourceRenderer.material == null) return new();
            UnityEngine.Color colorToCopy = sourceRenderer.material.color;
            foreach (SemantizationCore semantizationCore in targetObjects)
            {
                if (!semantizationCore.TryGetComponent(out Renderer renderer) || renderer.material == null) continue;
                renderer.material.color = colorToCopy;
            }
            return targetObjects;
        }
    }
}