// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.GeoData;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sven.Content
{
    /// <summary>
    /// Mapped properties to semantize.
    /// </summary>
    public static class MapppedProperties
    {
        /// <summary>
        /// Description of a property.
        /// </summary>
        public class PropertyDescription
        {
            /// <summary>
            /// Name of the property.
            /// </summary>
            public string TypeName { get; set; }
            /// <summary>
            /// Nested properties of the property.
            /// </summary>
            public List<string> NestedProperties { get; set; }

            public PropertyDescription(string typeName, List<string> nestedProperties)
            {
                TypeName = typeName;
                NestedProperties = nestedProperties;
            }
        }

        /// <summary>
        /// Values of the properties of the components.
        /// </summary>
        private static Dictionary<Type, PropertyDescription> Value { get; } = new()
        {
            { typeof(Matrix4x4), new PropertyDescription("Matrix4x4", new List<string> { "m00", "m01", "m02", "m03", "m10", "m11", "m12", "m13", "m20", "m21", "m22", "m23", "m30", "m31", "m32", "m33" }) },
            { typeof(Quaternion), new PropertyDescription("Quaternion", new List<string> { "x", "y", "z", "w" }) },
            { typeof(Vector4), new PropertyDescription("Vector4", new List<string> { "x", "y", "z", "w" }) },
            { typeof(Vector3), new PropertyDescription("Vector3", new List<string> { "x", "y", "z" }) },
            { typeof(Vector2), new PropertyDescription("Vector2", new List<string> { "x", "y" }) },
            { typeof(Color), new PropertyDescription("Color", new List<string> { "r", "g", "b", "a"}) },
            { typeof(object), new PropertyDescription("Primitive", new List<string> { "value" }) },
            { typeof(GeoWKT), new PropertyDescription("geo:Geometry", new List<string> { "geo:asWKT" }) },
        };

        /// <summary>
        /// Check if the property is mapped.
        /// </summary>
        /// <param name="type">Type of the property.</param>
        /// <returns>True if the property is mapped.</returns>
        public static bool ContainsKey(Type type)
        {
            return Value.ContainsKey(type);
        }

        /// <summary>
        /// Get the values of the properties.
        /// </summary>
        /// <param name="type">Type of the property.</param>
        /// <returns>Values of the properties of the component.</returns>
        public static PropertyDescription GetValue(Type type)
        {
            if (Value.TryGetValue(type, out var value))
                return value;
            return Value[typeof(object)];
        }

        /// <summary>
        /// Get the values of the properties.
        /// </summary>
        /// <param name="type">Type of the property.</param>
        /// <param name="propertyDescription">Values of the properties of the component.</param>
        /// <returns>True if the property was found, false otherwise.</returns>
        public static bool TryGetValue(Type type, out PropertyDescription propertyDescription)
        {
            if (Value.TryGetValue(type, out propertyDescription))
                return true;
            return false;
        }

        /// <summary>
        /// Check if a property has a nested property.
        /// </summary>
        /// <param name="type">Type of the property.</param>
        /// <param name="propertyNestedName">Name of the nested property.</param>
        /// <returns>True if the property has the nested property, false otherwise.</returns>
        public static bool HasNestedProperty(Type type, string propertyNestedName)
        {
            if (Value.TryGetValue(type, out var propertyDescription))
                return propertyDescription.NestedProperties.Contains(propertyNestedName);
            return false;
        }

        public static Type GetType(string typeName)
        {
            if (Value.Values.Any(x => x.TypeName == typeName))
                return Value.FirstOrDefault(x => x.Value.TypeName == typeName).Key;
            return typeof(object);
        }

        public static List<string> GetNestedProperties(Type type)
        {
            if (Value.TryGetValue(type, out var propertyDescription))
                return propertyDescription.NestedProperties;
            return Value[typeof(object)].NestedProperties;
        }
    }
}