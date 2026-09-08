using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// État refusé, ajouté par un client dont la contrainte alimentaire est violée.
    ///
    /// Cette classe n'est JAMAIS ajoutée comme composant : elle existe pour que
    /// ISemanticAnnotation.GetSemanticTypes("sven:Refused") rende « sven:Refused » ET
    /// « sven:FoodState » par réflexion sur sa hiérarchie — le même mécanisme que la planche
    /// et la plaque, donc une seule résolution des parents dans tout le projet. C'est aussi ce
    /// qui permet à CustomerOrder, dans l'assembly SC4VE, de l'atteindre sans dépendance de
    /// compilation.
    /// </summary>
    public class Refused : FoodState, IComponentMapping, ISemanticAnnotation
    {
        public static new string SemanticTypeName => "sven:Refused";

        public static ComponentMapping ComponentMapping()
        {
            return new("RefusedComponent",
                new List<Delegate>
                {
                    (Func<Refused, ComponentProperty>)(x => new ComponentProperty("enabled", () => x.enabled, value => x.enabled = value.ToString() == "true", 1)),
                });
        }
    }
}
