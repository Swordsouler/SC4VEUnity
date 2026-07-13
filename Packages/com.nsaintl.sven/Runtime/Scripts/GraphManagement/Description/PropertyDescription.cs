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
    /// Graph that contains the property description.
    /// </summary>
    public class PropertyDescription
    {
        /// <summary>
        /// UUID of the property.
        /// </summary>
        public string UUID { get; set; }
        /// <summary>
        /// Name of the property.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Type of the property.
        /// </summary>
        public Type Type { get; set; }
        /// <summary>
        /// Values of the property.
        /// </summary>
        public Dictionary<string, object> Values { get; set; }

        public object Value
        {
            get
            {
                if (Values.Count == 1 && Values.ContainsKey("value"))
                {
                    return Values["value"];
                }
                else
                {
                    // create an instance of the property type
                    object propertyValue = Activator.CreateInstance(Type);
                    // set the values of the property
                    foreach (KeyValuePair<string, object> value in Values)
                    {
                        try
                        {
                            Type.GetField(value.Key)?.SetValue(propertyValue, value.Value);
                            Type.GetProperty(value.Key)?.SetValue(propertyValue, value.Value);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"Could not set property {value.Key} of type {Type}: {e}");
                        }
                    }
                    return propertyValue;
                }
            }
        }

        public PropertyDescription(string uuid, string name, Type type)
        {
            UUID = uuid;
            Name = name;
            Type = type;
            Values = new();
        }

        /// <summary>
        /// ToString method.
        /// </summary>
        /// <returns>String representation of the property description.</returns>
        public override string ToString()
        {
            if (Values == null)
            {
                return $"{Name} ({Type.Name}): [Values dictionary is null]";
            }

            var valueStrings = Values.Select(x =>
            {
                string keyStr = x.Key ?? "NULL_KEY";
                string valStr = x.Value?.ToString() ?? "null";
                return $"{keyStr}: {valStr}";
            });

            return $"{Name} ({Type.Name}): [{string.Join(", ", valueStrings)}]";
        }
    }
}
