using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>Serveur autonome</summary>
    public class Waiter : Person, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Waiter";

        public static ComponentMapping ComponentMapping()
        {
            return new("WaiterComponent",
                new List<Delegate>
                {
                    (Func<Waiter, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
