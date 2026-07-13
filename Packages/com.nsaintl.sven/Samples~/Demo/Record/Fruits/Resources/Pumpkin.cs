using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sven.Demo
{
    public class Pumpkin : Vegetable, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Pumpkin";

        public static ComponentMapping ComponentMapping()
        {
            return new("PumpkinComponent",
                new List<Delegate>
                {
                    (Func<Pumpkin, ComponentProperty>)(pumpkin => new ComponentProperty("enabled", () => pumpkin.enabled, value => pumpkin.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
