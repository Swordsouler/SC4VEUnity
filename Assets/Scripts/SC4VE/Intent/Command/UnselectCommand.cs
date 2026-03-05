using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable]//, CommandDescription("Désélectionne des objets. Paramètres: SelectionParameter.")]
    public class UnselectCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            foreach (SemantizationCore semantizationCore in objects)
            {
                throw new NotImplementedException("Select functionality is not implemented yet.");
            }
            return objects;
        }
    }
}