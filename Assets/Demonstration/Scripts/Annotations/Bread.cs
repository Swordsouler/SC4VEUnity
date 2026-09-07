using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Pain</summary>
    public class Bread : Bakery, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Bread";

        public static ComponentMapping ComponentMapping()
        {
            return new("BreadComponent",
                new List<Delegate>
                {
                    (Func<Bread, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
