using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Saumon</summary>
    public class Salmon : Fish, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Salmon";

        public static ComponentMapping ComponentMapping()
        {
            return new("SalmonComponent",
                new List<Delegate>
                {
                    (Func<Salmon, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
