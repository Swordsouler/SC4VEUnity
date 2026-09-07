using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Planche a decouper</summary>
    public class CuttingBoard : Station, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:CuttingBoard";

        public static ComponentMapping ComponentMapping()
        {
            return new("CuttingBoardComponent",
                new List<Delegate>
                {
                    (Func<CuttingBoard, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
