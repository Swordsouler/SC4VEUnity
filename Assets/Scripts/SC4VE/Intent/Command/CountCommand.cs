using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("combien", "compte les", "compter", "nombre de")]
    [Serializable, CommandDescription("Compte les objets correspondant au filtre et l'affiche dans la console. Paramètres: SelectionParameter.")]
    public class CountCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            Debug.Log($"[Count] {objects.Count} objet(s) trouvé(s).");
            return objects;
        }
    }
}
