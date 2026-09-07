using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Boeuf (steak)</summary>
    public class Beef : Meat, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Beef";

        public static ComponentMapping ComponentMapping()
        {
            return new("BeefComponent",
                new List<Delegate>
                {
                    (Func<Beef, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
