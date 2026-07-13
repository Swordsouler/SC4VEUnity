using Sven.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("sélectionne tout", "sélectionner tout", "tout sélectionner",
                       "sélectionne tous", "sélectionne toutes")]
    [Serializable, CommandDescription("Sélectionne tous les objets actifs de la scène. Pas de paramètre.")]
    public class SelectAllCommand : Command
    {
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
            => new List<Parameter>();

        public override List<SemantizationCore> Execute()
        {
            var all = UnityEngine.Object.FindObjectsByType<SemantizationCore>()
                .ToList();
            Debug.Log($"[SelectAll] {all.Count} objet(s) sélectionné(s).");
            return all;
        }
    }
}
