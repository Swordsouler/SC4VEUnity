using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Assiette, seul contenant de service</summary>
    public class Plate : Container, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Plate";

        public static ComponentMapping ComponentMapping()
        {
            return new("PlateComponent",
                new List<Delegate>
                {
                    (Func<Plate, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
