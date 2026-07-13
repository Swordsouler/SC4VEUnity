// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sven.GraphManagement.Description
{
    /// <summary>
    /// GameObject description.
    /// </summary>
    public class GameObjectDescription
    {
        /// <summary>
        /// UUID of the GameObject.
        /// </summary>
        public string UUID { get; set; }

        /// <summary>
        /// GameObject.
        /// </summary>
        public GameObject GameObject { get; set; }

        /// <summary>
        /// Active state of the GameObject.
        /// </summary>
        public bool Active;

        /// <summary>
        /// Layer of the GameObject.
        /// </summary>
        public string Layer;

        /// <summary>
        /// Tag of the GameObject.
        /// </summary>
        public string Tag;

        /// <summary>
        /// Name of the GameObject.
        /// </summary>
        public string Name;

        /// <summary>
        /// Components of the GameObject.
        /// </summary>
        public Dictionary<string, ComponentDescription> Components { get; set; }

        public GameObjectDescription(string uuid)
        {
            UUID = uuid;
            Components = new();
        }

        public GameObjectDescription(string uuid, Dictionary<string, ComponentDescription> components)
        {
            UUID = uuid;
            Components = components;
        }

        public GameObjectDescription(string uuid, GameObject gameObject, Dictionary<string, ComponentDescription> components)
        {
            UUID = uuid;
            GameObject = gameObject;
            Components = components;
        }

        /// <summary>
        /// ToString method.
        /// </summary>
        /// <returns>String representation of the property description.</returns>
        public override string ToString()
        {
            return string.Join("\n", Components.Select(x => $"{x.Key} ({x.Value.Type})\n{x.Value}"));
        }
    }
}
