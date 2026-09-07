using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Demonstration
{
    /// <summary>Couteau — ustensile de découpe.</summary>
    public class Knife : MonoBehaviour, IComponentMapping, ISemanticAnnotation
    {
        public static string SemanticTypeName => "sven:Knife";

        public static ComponentMapping ComponentMapping()
        {
            return new("KnifeComponent",
                new List<Delegate>
                {
                    (Func<Knife, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
