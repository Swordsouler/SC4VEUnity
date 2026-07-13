// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.GeoData;
using Sven.GraphManagement;
using Sven.OwlTime;
using Sven.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Nodes;
using VDS.RDF.Parsing;

namespace Sven.Content
{
    /// <summary>
    /// Semantization extensions for Unity components.
    /// </summary>
    public static class SemantizationExtensions
    {
        #region UUID

        /// <summary>
        /// Generates a unique identifier for the component.
        /// </summary>
        /// <returns>Unique identifier.</returns>
        private static readonly ConcurrentDictionary<Component, (string, Interval)> componentUUIDs = new();

        /// <summary>
        /// Generates a unique identifier for the component.
        /// </summary>
        /// <param name="component">Component to generate the identifier.</param>
        private static void GenerateUUID(this Component component)
        {
            string UUID = Guid.NewGuid().ToString();
            Interval interval = new();
            componentUUIDs[component] = (UUID, interval);
        }

        /// <summary>
        /// Destroys the identifier for the component. WARNING: Not doing this will cause memory leaks.
        /// </summary>
        /// <param name="component">Component to destroy the identifier.</param>
        public static void DestroyUUID(this Component component)
        {
            if (componentUUIDs.ContainsKey(component))
                componentUUIDs.TryRemove(component, out _);
        }

        /// <summary>
        /// Gets the identifier for the component.
        /// </summary>
        /// <param name="component">Component to get the identifier.</param>
        /// <returns>Unique identifier.</returns>
        public static string GetUUID(this Component component)
        {
            try
            {
                if (!componentUUIDs.ContainsKey(component)) component.GenerateUUID();
                return componentUUIDs[component].Item1;
            }
            catch (KeyNotFoundException e)
            {
                Debug.LogError(e);
                return "";
            }
        }

        public static void AddUUID(this Component component, string UUID)
        {
            if (componentUUIDs.ContainsKey(component))
            {
                Debug.LogWarning("Component already has a UUID. Overwriting it.");
                componentUUIDs[component] = (UUID, new Interval());
            }
            else
            {
                componentUUIDs.TryAdd(component, (UUID, new Interval()));
            }
        }

        /// <summary>
        /// Gets the component by the identifier.
        /// </summary>
        /// <param name="UUID">Identifier to get the component.</param>
        /// <returns>Component.</returns>
        public static Component GetComponentByUUID(string UUID)
        {
            return componentUUIDs.FirstOrDefault(x => x.Value.Item1 == UUID).Key;
        }

        public static bool TryGetComponentByUUID(string UUID, out Component component)
        {
            var kvp = componentUUIDs.FirstOrDefault(x => x.Value.Item1 == UUID);
            if (kvp.Key != null)
            {
                component = kvp.Key;
                return true;
            }
            component = null;
            return false;
        }

        /// <summary>
        /// Gets the interval for the component.
        /// </summary>
        /// <param name="component">Component to get the interval.</param>
        /// <returns>Interval.</returns>
        public static Interval GetInterval(this Component component)
        {
            try
            {
                if (!componentUUIDs.ContainsKey(component)) component.GenerateUUID();
                return componentUUIDs.TryGetValue(component, out (string, Interval) c) ? c.Item2 : throw new KeyNotFoundException();
            }
            catch (KeyNotFoundException e)
            {
                Debug.LogError(e);
                return new Interval();
            }
        }

        #endregion

        #region Observers

        /// <summary>
        /// List of properties to ignore when semantizing.
        /// </summary>
        private static readonly List<string> ignoredProperties = new()
        {
            "destroyCancellationToken",
            "useGUILayout",
            "runInEditMode",
            "isActiveAndEnabled",
            "hideFlags",
            "didStart",
            "didAwake",
            "name"
        };

        /// <summary>
        /// Get all the properties of the component.
        /// </summary>
        /// <param name="component">Component to get the properties.</param>
        /// <returns>List of all properties that can be observed and semantized without the ignored properties.</returns>
        private static List<Property> GetAllProperties(this Component component)
        {
            List<Property> properties = new();
            foreach (var field in component.GetType().GetProperties())
            {
                if (field.DeclaringType == typeof(Component)) continue;
                if (ignoredProperties.Contains(field.Name)) continue;
                // does not support arrays
                if (field.PropertyType.IsArray) continue;
                properties.Add(new Property(field.Name, () => field.GetValue(component)));
            }
            return properties;
        }

        /// <summary>
        /// Get properties of the component.
        /// </summary>
        /// <param name="component">Component to get the properties.</param>
        /// <returns>List of properties that can be observed and semantized.</returns>
        private static List<Property> GetProperties(this Component component)
        {
            Type type = component.GetType();
            if (MapppedComponents.ContainsKey(type))
            {
                List<Property> properties = new();
                foreach (Delegate del in MapppedComponents.GetValue(type).Properties)
                {
                    ComponentProperty propertyDescription = del.DynamicInvoke(component) as ComponentProperty;
                    properties.Add(new Property(propertyDescription.PredicateName, propertyDescription.Getter, propertyDescription.OnSemanticize, propertyDescription.SimplifiedName));
                }
                return properties;
            }
            else return GetAllProperties(component);
        }

        #endregion

        #region Semantize

        /// <summary>
        /// Semantize the component and observe his properties.
        /// </summary>
        /// <param name="component">Component to semantize.</param>
        /// <param name="graphBuffer">Graph buffer to semantize the component.</param>
        /// <returns>List of properties that have been semantized.</returns>
        public static List<Property> SemanticObserve(this Component component)
        {
            if (!component.gameObject.TryGetComponent(out SemantizationCore semantizationCore))
            {
                Debug.LogError("SemantizationCore not found in the GameObject. Aborting semantization of the component " + component.GetType().Name + " in the GameObject " + component.gameObject.name);
                throw new NullReferenceException();
            }

            IUriNode gameObjectNode = GraphManager.CreateUriNode(":" + semantizationCore.GetUUID());
            IUriNode componentNode = GraphManager.CreateUriNode(":" + component.GetUUID());
            IUriNode componentTypeNode = GraphManager.CreateUriNode(component.GetRdfType());

            GraphManager.Assert(new Triple(gameObjectNode, GraphManager.CreateUriNode("sven:component"), componentNode));
            GraphManager.Assert(new Triple(componentNode, GraphManager.CreateUriNode("rdf:type"), componentTypeNode));
            GraphManager.Assert(new Triple(componentNode, GraphManager.CreateUriNode("sven:exactType"), componentTypeNode));

            List<Property> properties = component.GetProperties();
            foreach (Property property in properties)
            {
                property.SemanticObserve(component);
                if (SvenSettings.Debug)
                    Debug.Log("Observing property (" + semantizationCore.name + ")." + component.GetType().Name + "." + property.Name);
            }

            Interval interval = component.GetInterval();
            interval.Start(GraphManager.CurrentInstant);
            IUriNode intervalNode = interval.Semanticize();
            GraphManager.Assert(new Triple(componentNode, GraphManager.CreateUriNode("sven:hasTemporalExtent"), intervalNode));

            return properties;
        }

        /// <summary>
        /// Get the RDF type of the component.
        /// </summary>
        /// <param name="component">Component to get the RDF type.</param>
        /// <returns>RDF type of the component.</returns>
        public static string GetRdfType(this Component component)
        {
            if (MapppedComponents.TryGetValue(component.GetType(), out var value) && value != null)
                return value.TypeName.Contains(":") ? value.TypeName : "sven:" + value.TypeName;
            else return "sven:" + component.GetType().Name;
        }

        #endregion

        #region Data Types

        public static object ToValue(this IValuedNode node)
        {
            return node.EffectiveType.Split("#")[1] switch
            {
                "string" => node.AsString(),
                // AsInteger() retourne un Int64 : on rétrécit en Int32 pour que les setters `(int)value` (boxés en int)
                // ne lèvent pas InvalidCastException au rejeu (un long boxé ne se déballe pas en int).
                "int" => (int)node.AsInteger(),
                "float" => node.AsFloat(),
                "double" => node.AsDouble(),
                // Le fragment du datatype XSD est "boolean", pas "bool" : sans ça, ce cas était du code mort.
                "boolean" => node.AsBoolean(),
                "dateTime" => node.AsDateTime(),
                _ => node.AsString(),
            };
        }

        /// <summary>
        /// Get the XML Schema data type for the object.
        /// </summary>
        /// <param name="obj">Object to get the XML Schema data type.</param>
        /// <returns>XML Schema data type.</returns>
        public static string GetXmlSchemaTypes(this object obj)
        {
            Type type = obj.GetType();
            return type switch
            {
                Type t when t == typeof(bool) => XmlSpecsHelper.XmlSchemaDataTypeBoolean,
                Type t when t == typeof(int) => XmlSpecsHelper.XmlSchemaDataTypeInt,
                Type t when t == typeof(float) => XmlSpecsHelper.XmlSchemaDataTypeFloat,
                Type t when t == typeof(GeoWKT) => "http://www.opengis.net/ont/geosparql#wktLiteral",
                _ => XmlSpecsHelper.XmlSchemaDataTypeString,
            };
        }

        public static string ToRdfString(this object obj)
        {
            Type type = obj.GetType();
            return type switch
            {
                Type t when t == typeof(bool) => obj.ToString().ToLower(),
                // "R" (round-trip) produit une forme lexicale xsd:float/xsd:double VALIDE : pas de séparateur de
                // milliers (contrairement à "N4" qui donnait "1,000.0000" pour 1000) et précision complète.
                Type t when t == typeof(float) => ((float)obj).ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                Type t when t == typeof(double) => ((double)obj).ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                Type t when t == typeof(int) => ((int)obj).ToString(System.Globalization.CultureInfo.InvariantCulture),
                // Repli en culture invariante (évite "3,14" en fr-FR pour tout autre type IConvertible/IFormattable).
                _ => System.Convert.ToString(obj, System.Globalization.CultureInfo.InvariantCulture),
            };
        }

        /// <summary>
        /// Get the nested values of the object.
        /// </summary>
        /// <param name="obj">Object to get the nested values.</param>
        /// <returns>Nested values.</returns>
        public static Dictionary<string, object> GetSemantizableValues(this object obj)
        {
            Dictionary<string, object> values = new();
            Type type = obj.GetType();
            if (type.IsPrimitive || type == typeof(string))
            {
                values.Add("value", obj);
            }
            else
            {
                if (type == typeof(GeoWKT))
                {
                    values.Add("geo:asWKT", obj);
                    return values;
                }
                else if (!MapppedProperties.ContainsKey(type))
                {
                    Debug.LogWarning($"Type {type} is not supported for nested values. Returning the object as a string.");
                    values.Add("value", obj.ToString());
                    return values;
                }
                foreach (string fieldName in MapppedProperties.GetValue(type).NestedProperties)
                {
                    FieldInfo field = type.GetField(fieldName);
                    if (field != null)
                    {
                        values.Add(fieldName, field.GetValue(obj));
                    }
                    else
                    {
                        PropertyInfo property = type.GetProperty(fieldName);
                        if (property != null)
                        {
                            values.Add(fieldName, property.GetValue(obj));
                        }
                    }
                }
            }
            return values;
        }

        #endregion
    }
}