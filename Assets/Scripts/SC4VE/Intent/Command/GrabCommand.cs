using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("attrape", "attraper", "prends", "prendre", "grab", "saisit", "saisir", "empare", "emparer")]
    [Serializable, CommandDescription("Saisit les objets. Paramètres: SelectionParameter.")]
    public class GrabCommand : Command
    {
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
            => new List<Parameter> { ctx.BuildSelectionParameter(), ctx.BuildGrabPointParameter() };

        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            foreach (SemantizationCore semantizationCore in objects)
            {
                Debug.Log($"Grabbing object {semantizationCore.GetUUID()}");
            }
            return objects;
        }
    }
}