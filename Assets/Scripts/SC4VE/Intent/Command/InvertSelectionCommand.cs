using Sven.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("inverse la sélection", "inverser la sélection", "inversion")]
    [Serializable, CommandDescription("Inverse la sélection courante. Pas de paramètre.")]
    public class InvertSelectionCommand : Command
    {
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
            => new List<Parameter>();

        public override List<SemantizationCore> Execute()
        {
            var selectedIds = new HashSet<string>(SelectionManager.SelectedIds);
            var inverted = UnityEngine.Object.FindObjectsByType<SemantizationCore>(FindObjectsSortMode.None)
                .Where(o => !selectedIds.Contains(o.GetUUID()))
                .ToList();
            SelectionManager.Clear();
            SelectionManager.Select(inverted);
            Debug.Log($"[InvertSelection] {inverted.Count} objet(s) maintenant sélectionné(s).");
            return SelectionManager.Selected.ToList();
        }
    }
}
