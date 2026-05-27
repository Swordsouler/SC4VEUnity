using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("rétablis", "rétablir", "redo", "refais", "refaire")]
    [Serializable, CommandDescription("Rétablit la dernière action annulée. Pas de paramètre.")]
    public class RedoCommand : Command
    {
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
            => new List<Parameter>();

        public override List<SemantizationCore> Execute()
        {
            CommandHistory.Redo();
            return new List<SemantizationCore>();
        }
    }
}
