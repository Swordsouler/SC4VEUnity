using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Client</summary>
    public class Customer : Person, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Customer";

        public static ComponentMapping ComponentMapping()
        {
            return new("CustomerComponent",
                new List<Delegate>
                {
                    (Func<Customer, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
