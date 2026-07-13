// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using VDS.RDF;

namespace Sven.Content
{
    /// <summary>
    /// Description of a property.
    /// </summary>
    public class ComponentProperty
    {
        /// <summary>
        /// Name of the property.
        /// </summary>
        public string PredicateName { get; set; }
        /// <summary>
        /// Getter of the property.
        /// </summary>
        public Func<object> Getter { get; set; }
        /// <summary>
        /// Setter of the property.
        /// </summary>
        public Action<object> Setter { get; set; }
        /// <summary>
        /// Priority of the property. (closer to 0 is higher priority)
        /// </summary>
        public int Priority { get; set; }
        /// <summary>
        /// Getter of the property.
        /// </summary>
        public Action<IUriNode> OnSemanticize { get; set; }
        /// <summary>
        /// Simplified name of the property.
        /// </summary>
        public string SimplifiedName { get; set; }

        public ComponentProperty(string predicateName, Func<object> getter, Action<object> setter, int priority, Action<IUriNode> onSemanticize = null, string simplifiedName = "")
        {
            PredicateName = predicateName;
            Getter = getter;
            Setter = setter;
            Priority = priority;
            OnSemanticize = onSemanticize;
            SimplifiedName = simplifiedName;
        }

        public ComponentProperty(string predicateName, Func<object> getter, Action<object> setter, int priority, Action<IUriNode> onSemanticize = null)
        {
            PredicateName = predicateName;
            Getter = getter;
            Setter = setter;
            SimplifiedName = "";
            Priority = priority;
            OnSemanticize = onSemanticize;
        }

        public ComponentProperty(string predicateName, Func<object> getter, Action<object> setter, int priority)
        {
            PredicateName = predicateName;
            Getter = getter;
            Setter = setter;
            SimplifiedName = "";
            Priority = priority;
            OnSemanticize = null;
        }
    }
}