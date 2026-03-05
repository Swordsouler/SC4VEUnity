using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription("Duplique les objets sélectionnés. Paramètres: SelectionParameter.")]
    public class DuplicateCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;

            foreach (SemantizationCore semantizationCore in objects)
            {
                GameObject duplicatedGameObject = UnityEngine.Object.Instantiate(semantizationCore.gameObject);
                // position 1 above the original object to avoid overlap
                duplicatedGameObject.transform.position += Vector3.up;
            }

            return objects;
        }
    }
}