using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Table de la salle, jamais numerotee</summary>
    public class Table : Furniture, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Table";

        public static ComponentMapping ComponentMapping()
        {
            return new("TableComponent",
                new List<Delegate>
                {
                    (Func<Table, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
