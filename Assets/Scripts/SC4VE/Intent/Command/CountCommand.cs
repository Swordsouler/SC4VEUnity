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
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            Debug.Log($"[Count] {objects.Count} objet(s) trouvé(s).");
            Speak(objects.Count > 1
                ? $"{objects.Count} objets trouvés."
                : $"{objects.Count} objet trouvé.");
            return objects;
        }
    }
}
