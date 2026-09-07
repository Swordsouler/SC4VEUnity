using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Etat coupe, ajoute par la planche a decouper</summary>
    public class Sliced : FoodState, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Sliced";

        public static ComponentMapping ComponentMapping()
        {
            return new("SlicedComponent",
                new List<Delegate>
                {
                    (Func<Sliced, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
