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

        public override List<SemantizationCore> Execute()
        {
            CommandHistory.Undo();
            return new List<SemantizationCore>();
        }
    }
}
