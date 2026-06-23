using Sven.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("désélectionne", "désélectionner", "unselect", "démarque", "démarquer")]
    [Serializable, CommandDescription("Retire des objets de la sélection. Sans cible (« désélectionne tout »), vide toute la sélection. Paramètres: SelectionParameter (optionnel).")]
    public class UnselectCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            bool hasFilter = SelectionParameter?.Filters != null && SelectionParameter.Filters.Count > 0;
            if (!hasFilter)
            {
                SelectionManager.Clear();
                Debug.Log("[Unselect] Sélection vidée.");
            }
            else
            {
                List<SemantizationCore> objects = SelectionParameter.Objects ?? new();
                SelectionManager.Deselect(objects);
                Debug.Log($"[Unselect] -{objects.Count} → {SelectionManager.Selected.Count} restant(s).");
            }
            return SelectionManager.Selected.ToList();
        }
    }
}
