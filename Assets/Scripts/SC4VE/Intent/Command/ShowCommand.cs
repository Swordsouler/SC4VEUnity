using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription("Affiche les objets. Paramètres: SelectionParameter.")]
    [RuleBasedTriggers("rend visible", "rends visible", "montre", "affiche", "révèle",
                       "démasque", "montrer", "afficher", "révéler", "démasquer")]
    public class ShowCommand : Command
    {
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, semantizationCore =>
            {
                if (!semantizationCore.TryGetComponent(out Renderer renderer)) return null;
                Renderer captured = renderer;
                bool prev = renderer.enabled;
                renderer.enabled = true;
                return (() => { if (captured != null) captured.enabled = prev; },
                        () => { if (captured != null) captured.enabled = true; });
            });
        }
    }
}