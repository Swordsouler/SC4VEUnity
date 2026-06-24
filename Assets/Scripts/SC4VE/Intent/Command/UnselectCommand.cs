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

        // Désélectionner gère lui-même l'absence de cible (vide toute la sélection) → pas de repli.
        protected override bool FallbackToSelectionWhenEmpty => false;

        public override List<SemantizationCore> Execute()
        {
            bool hasFilter = SelectionParameter?.Filters != null && SelectionParameter.Filters.Count > 0;
            if (!hasFilter)
            {
                Debug.Log("[Unselect] Sélection vidée.");
                return new(); // → ResolveCommands applique une sélection vide
            }

            var toRemove = new HashSet<string>((SelectionParameter.Objects ?? new()).Select(o => o.GetUUID()));
            List<SemantizationCore> result = SelectionManager.Selected
                .Where(o => !toRemove.Contains(o.GetUUID()))
                .ToList();
            Debug.Log($"[Unselect] -{toRemove.Count} → {result.Count} restant(s).");
            return result;
        }
    }
}
