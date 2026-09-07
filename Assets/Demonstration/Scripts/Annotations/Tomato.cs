using Sven.Content;
using Sven.Demo;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Tomate</summary>
    public class Tomato : Vegetable, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Tomato";

        public static ComponentMapping ComponentMapping()
        {
            return new("TomatoComponent",
                new List<Delegate>
                {
                    (Func<Tomato, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
