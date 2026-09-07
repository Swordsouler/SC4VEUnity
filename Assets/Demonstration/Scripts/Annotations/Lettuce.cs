using Sven.Content;
using Sven.Demo;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Laitue</summary>
    public class Lettuce : Vegetable, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Lettuce";

        public static ComponentMapping ComponentMapping()
        {
            return new("LettuceComponent",
                new List<Delegate>
                {
                    (Func<Lettuce, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
