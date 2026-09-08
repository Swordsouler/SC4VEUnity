using Sven.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("sélectionne aussi", "sélectionnez aussi", "sélectionner aussi",
                       "sélectionne également", "sélectionnez également",
                       "rajoute", "rajouter", "rajoutez",
                       "also select", "select also", "add to selection", "add to the selection")]
    [Serializable, CommandDescription(
        "Ajoute les objets ciblés à la sélection persistante, sans retirer ceux qui y sont déjà " +
        "(« sélectionne aussi les tomates », « ajoute les bananes à la sélection », « rajoute ça »). " +
        "Paramètres: SelectionParameter (les objets à ajouter).")]
    public class AddToSelectionCommand : Command
    {
        // Comme SelectCommand : la cible doit être désignée (« Sur quels objets ? » sinon) —
        // le repli sur la sélection courante n'aurait aucun sens pour un ajout à celle-ci.
        protected override bool FallbackToSelectionWhenEmpty => false;

        // L'union est appliquée par ResolveCommands à partir du retour, comme SelectCommand.
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> added = SelectionParameter?.Objects ?? new();

            // Base d'union : LastObjects, pas SelectionManager.Selected. Les deux sont
            // identiques après chaque commande aboutie, SAUF pendant une désambiguïsation
            // (« rajoute la banane » avec deux bananes) : la question remplace la surbrillance
            // par les candidats, et l'union sur la sélection courante rendrait les candidats
            // au lieu de la sélection d'origine plus l'élu.
            List<SemantizationCore> result = (LastObjects ?? new List<SemantizationCore>())
                .Concat(added)
                .Where(o => o != null)
                .GroupBy(o => o.GetUUID())
                .Select(g => g.First())
                .ToList();

            Debug.Log($"[AddToSelection] +{added.Count} → {result.Count} objet(s) sélectionné(s).");
            return result;
        }
    }
}
