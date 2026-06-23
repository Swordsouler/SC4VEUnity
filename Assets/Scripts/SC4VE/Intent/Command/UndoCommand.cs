using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("annule", "annuler", "undo", "défais", "défaire")]
    [Serializable, CommandDescription("Annule la dernière action. Pas de paramètre.")]
    public class UndoCommand : Command
    {
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
            => new List<Parameter>();

        // Retourne les objets affectés par l'action annulée → ResolveCommands les re-sélectionne.
        public override List<SemantizationCore> Execute()
            => CommandHistory.Undo();
    }
}
