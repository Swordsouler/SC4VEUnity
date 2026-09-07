using Sven.Content;
using Sven.Demo;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Pomme de terre</summary>
    public class Potato : Vegetable, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Potato";

        public static ComponentMapping ComponentMapping()
        {
            return new("PotatoComponent",
                new List<Delegate>
                {
                    (Func<Potato, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
