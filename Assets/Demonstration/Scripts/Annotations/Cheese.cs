using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Fromage</summary>
    public class Cheese : Dairy, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Cheese";

        public static ComponentMapping ComponentMapping()
        {
            return new("CheeseComponent",
                new List<Delegate>
                {
                    (Func<Cheese, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
