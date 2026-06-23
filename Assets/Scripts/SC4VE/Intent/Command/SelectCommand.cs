using Sven.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("sélectionne", "sélectionner", "select", "choisis", "choisir", "marque", "marquer")]
    [Serializable, CommandDescription("Ajoute des objets à la sélection persistante (contour visuel). Paramètres: SelectionParameter.")]
    public class SelectCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            SelectionManager.Select(objects);
            Debug.Log($"[Select] +{objects.Count} → {SelectionManager.Selected.Count} sélectionné(s).");
            return SelectionManager.Selected.ToList();
        }
    }
}
