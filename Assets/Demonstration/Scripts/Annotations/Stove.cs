using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Plaque de cuisson</summary>
    public class Stove : Station, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Stove";

        public static ComponentMapping ComponentMapping()
        {
            return new("StoveComponent",
                new List<Delegate>
                {
                    (Func<Stove, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
