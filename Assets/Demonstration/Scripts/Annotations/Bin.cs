using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Poubelle</summary>
    public class Bin : Container, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Bin";

        public static ComponentMapping ComponentMapping()
        {
            return new("BinComponent",
                new List<Delegate>
                {
                    (Func<Bin, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
