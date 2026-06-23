using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("sélectionne", "sélectionner", "select", "choisis", "choisir", "marque", "marquer")]
    [Serializable, CommandDescription("Définit la sélection persistante (contour visuel) sur les objets ciblés. Paramètres: SelectionParameter.")]
    public class SelectCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        // La sélection effective est appliquée par ResolveCommands à partir du retour.
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            Debug.Log($"[Select] {objects.Count} objet(s) sélectionné(s).");
            return objects;
        }
    }
}
