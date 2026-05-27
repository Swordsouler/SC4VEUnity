using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("réinitialise la scène", "remet la scène", "reset la scène",
                       "tout réinitialiser", "remet tout", "restaure la scène")]
    [Serializable, CommandDescription("Remet tous les objets à leur état initial. Pas de paramètre.")]
    public class ResetSceneCommand : Command
    {
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
            => new List<Parameter>();

        public override List<SemantizationCore> Execute()
        {
            CommandHistory.Clear();
            OriginalStateStore.RestoreAll();
            return new List<SemantizationCore>();
        }
    }
}
