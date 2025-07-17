using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sven.Demo
{
    public class Carrot : Vegetable, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Carrot";

        public static ComponentMapping ComponentMapping()
        {
            return new("CarrotComponent",
                new List<Delegate>
                {
                    (Func<Carrot, ComponentProperty>)(carrot => new ComponentProperty("enabled", () => carrot.enabled, value => carrot.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
