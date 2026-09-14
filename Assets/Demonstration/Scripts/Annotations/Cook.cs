using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Cuisinier autonome</summary>
    public class Cook : Person, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Cook";

        public static ComponentMapping ComponentMapping()
        {
            return new("CookComponent",
                new List<Delegate>
                {
                    (Func<Cook, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
