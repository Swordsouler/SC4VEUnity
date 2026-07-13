// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sven.GraphManagement.Description
{
    /// <summary>
    /// Component description.
    /// </summary>
    public class ComponentDescription
    {
        /// <summary>
        /// UUID of the Component.
        /// </summary>
        public string UUID { get; set; }

        /// <summary>
        /// Component.
        /// </summary>
        public Component Component { get; set; }

        /// <summary>
        /// Type of the component.
        /// </summary>
        public Type Type { get; set; }

        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// Properties of the component.
        /// </summary>
        public Dictionary<string, PropertyDescription> Properties { get; set; }

        public ComponentDescription(string uuid, Type type)
        {
            UUID = uuid;
            Type = type;
            Properties = new();
        }

        public ComponentDescription(string uuid, Type type, int sortOrder)
        {
            UUID = uuid;
            Type = type;
            SortOrder = sortOrder;
            Properties = new();
        }

        public ComponentDescription(string uuid, Type type, int sortOrder, Dictionary<string, PropertyDescription> properties)
        {
            UUID = uuid;
            Type = type;
            SortOrder = sortOrder;
            Properties = properties;
        }

        public ComponentDescription(string uuid, Component component, int sortOrder, Dictionary<string, PropertyDescription> properties)
        {
            UUID = uuid;
            Component = component;
            SortOrder = sortOrder;
            Properties = properties;
        }

        /// <summary>
        /// ToString method.
        /// </summary>
        /// <returns>String representation of the property description.</returns>
        public override string ToString()
        {
            return string.Join("\n", Properties.Select(x => $"\t{x.Key} ({x.Value.Type}): ({x.Value})"));
        }
    }
}
