using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Etat cuit, ajoute par la plaque de cuisson</summary>
    public class Cooked : FoodState, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Cooked";

        public static ComponentMapping ComponentMapping()
        {
            return new("CookedComponent",
                new List<Delegate>
                {
                    (Func<Cooked, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
