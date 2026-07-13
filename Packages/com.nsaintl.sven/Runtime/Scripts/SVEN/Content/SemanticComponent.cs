// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sven.Content
{
    /// <summary>
    /// Description of the component to semantize.
    /// </summary>
    [Serializable]
    public class SemanticComponent
    {
        /// <summary>
        /// The component to semantize.
        /// </summary>
        [field: SerializeField]
        public Component Component { get; set; }
        /// <summary>
        /// The semantic processing mode of the GameObject.
        /// </summary>
        [field: SerializeField]
        public SemanticProcessingMode ProcessingMode { get; set; }

        /// <summary>
        /// Properties of the component to semantize.
        /// </summary>
        public List<Property> Properties { get; set; }
        /// <summary>
        /// Flag to check if the component has been semantized atleast once.
        /// </summary>
        public bool IsSemantized { get; set; }
    }
}