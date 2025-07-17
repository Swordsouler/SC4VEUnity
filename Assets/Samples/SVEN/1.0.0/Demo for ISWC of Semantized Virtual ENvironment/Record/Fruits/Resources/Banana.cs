using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sven.Demo
{
    public class Banana : Fruit, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Banana";

        public static ComponentMapping ComponentMapping()
        {
            return new("BananaComponent",
                new List<Delegate>
                {
                    (Func<Banana, ComponentProperty>)(banana => new ComponentProperty("enabled", () => banana.enabled, value => banana.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
