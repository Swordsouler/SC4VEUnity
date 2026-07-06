using Sven.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("copie la couleur", "colorie comme", "même couleur que", "copier la couleur")]
    [Serializable, CommandDescription("Applique la couleur d'un objet à un autre. Paramètres: SelectionParameter (cible), SelectionParameter (source). Attention, la cible doit toujours être le premier SelectionParameter, et la source doit être le second SelectionParameter")]
    public class ColorizeCopyCommand : Command
    {
        private SelectionParameter SelectionParameterTarget => GetParameter<SelectionParameter>(1);
        private SelectionParameter SelectionParameterSource => GetParameter<SelectionParameter>(2);

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> targetObjects = SelectionParameterTarget?.Objects ?? new();
            SemantizationCore sourceObject = SelectionParameterSource?.Objects?.FirstOrDefault();
            if (sourceObject == null || !sourceObject.TryGetComponent(out MeshRenderer sourceRenderer) || sourceRenderer.material == null)
            {
                Debug.LogWarning("[ColorizeCopy] Objet source introuvable ou sans matériau — commande ignorée.");
                return new();
            }
            UnityEngine.Color colorToCopy = sourceRenderer.material.color;
            return ExecuteReversible(targetObjects, obj =>
            {
                if (!obj.TryGetComponent(out Renderer renderer) || renderer.material == null) return null;
                UnityEngine.Color prev = renderer.material.color;
                renderer.material.color = colorToCopy;
                return (() => renderer.material.color = prev,
                        () => renderer.material.color = colorToCopy);
            });
        }
    }
}