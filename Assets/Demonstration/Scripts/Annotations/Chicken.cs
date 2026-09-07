using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Poulet</summary>
    public class Chicken : Meat, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Chicken";

        public static ComponentMapping ComponentMapping()
        {
            return new("ChickenComponent",
                new List<Delegate>
                {
                    (Func<Chicken, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
