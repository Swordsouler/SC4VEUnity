// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sven.Content
{
    /// <summary>
    /// Description of a component.
    /// </summary>
    public class ComponentMapping
    {
        /// <summary>
        /// Name of the component in the knowledge graph.
        /// </summary>
        public string TypeName { get; set; }
        /// <summary>
        /// Delegates of the properties of the component.
        /// </summary>
        /// <value></value>
        public List<Delegate> Properties { get; set; }
        public Dictionary<string, ComponentProperty> CachedProperties { get; set; }
        public int SortOrder { get; set; } = 0;

        public ComponentMapping(string typeName, List<Delegate> properties, int sortOrder = 0)
        {
            TypeName = typeName;
            Properties = properties;
            CachedProperties = new();
            SortOrder = sortOrder;
            foreach (Delegate property in properties)
            {
                Type propertyType = property.GetType().GetGenericArguments().FirstOrDefault();
                if (propertyType != null)
                {
                    object instance = null;
                    try
                    {
                        if (typeof(MonoBehaviour).IsAssignableFrom(propertyType))
                        {
                            if (SvenSettings.Debug) Debug.Log("Creating instance of MonoBehaviour: " + propertyType.Name);
                            GameObject tempGameObject = new("Temp_" + propertyType.Name);
                            instance = tempGameObject.AddComponent(propertyType);
                        }
                        else
                        {
                            if (SvenSettings.Debug) Debug.Log("Creating instance of " + propertyType.Name);
                            instance = Activator.CreateInstance(propertyType, nonPublic: true);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("Error creating instance of " + propertyType.Name + ": " + e);
                    }
                    finally
                    {
                        if (property.DynamicInvoke(instance) is ComponentProperty propertyDescription)
                        {
                            CachedProperties.Add(propertyDescription.PredicateName, propertyDescription);
                        }

                        if (instance is MonoBehaviour monoBehaviourInstance)
                            GameObject.DestroyImmediate(monoBehaviourInstance.gameObject);
                    }
                }
                else
                {
                    Debug.LogError("Property type is null or not a MonoBehaviour: " + propertyType?.Name);
                }
            }
        }
    }
}