// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.GraphManagement;
using Sven.OwlTime;
using Sven.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace Sven.Content
{
    /// <summary>
    /// Observes changes in a single property and triggers multiple callbacks when the property changes.
    /// </summary>
    public class Property : Resource
    {
        /// <summary>
        /// The parent component of the property.
        /// </summary>
        private Component parentComponent;

        /// <summary>
        /// The semantization core to semantize the property.
        /// </summary>
        private SemantizationCore parentObject;

        /// <summary>
        /// The parent object node of the property.
        /// </summary>
        private IUriNode ParentObjectNode => GraphManager.CreateUriNode(":" + parentObject.GetUUID());

        /// <summary>
        /// The parent component node of the property.
        /// </summary>
        private IUriNode ParentComponentNode => GraphManager.CreateUriNode(":" + parentComponent.GetUUID());

        /// <summary>
        /// The simplified name of the property to observe.
        /// </summary>
        private readonly string simplifiedName;

        /// <summary>
        /// The name of the property to observe.
        /// </summary>
        private readonly string name;

        /// <summary>
        /// Gets the name of the property to observe.
        /// </summary>
        public string Name => name;

        /// <summary>
        /// The last instant the property was semantized.
        /// </summary>
        private Instant lastSemantizedInstant;

        /// <summary>
        /// The interval of the property.
        /// </summary>
        private Interval interval;

        /// <summary>
        /// Represents a property to observe.
        /// </summary>
        private class ObservedProperty
        {
            /// <summary>
            /// A list of functions that return the values of the properties to observe.
            /// </summary>
            public Func<object> Getter;

            /// <summary>
            /// The callback to invoke when any of the observed properties change.
            /// </summary>
            public Action<IUriNode> Callback;

            /// <summary>
            /// The last known values of the observed properties.
            /// </summary>
            public object LastValue;
        }

        /// <summary>
        /// The property to observe.
        /// </summary>
        private readonly ObservedProperty observedProperty;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyObserver"/> class.
        /// </summary>
        /// <param name="getter">A function that returns the value of the property to observe.</param>
        public Property(string name, Func<object> getter, Action<IUriNode> callback = null, string simplifiedName = "")
        {
            this.name = name;
            this.simplifiedName = simplifiedName;
            observedProperty = new ObservedProperty
            {
                Getter = getter,
                LastValue = null,
                Callback = callback
            };
        }

        /// <summary>
        /// Initializes the semantization of the property.
        /// </summary>
        /// <param name="graphBuffer">The graph buffer to semantize the property.</param>
        /// <param name="parentNode">The parent node of the property.</param>
        public void SemanticObserve(Component parentComponent)
        {
            if (parentComponent == null)
            {
                Debug.LogWarning("You are trying to initialize the semantization of a property without a graph buffer or parent component.");
                return;
            }
            if (this.parentComponent != null)
            {
                Debug.LogWarning("You are trying to initialize the semantization of a property that is already initialized.");
                return;
            }
            this.parentComponent = parentComponent;
            parentObject = parentComponent.GetComponent<SemantizationCore>();
        }

        /// <summary>
        /// Checks if the observed property has changed and invokes the callbacks if it has.
        /// Runs synchronously on the main thread: the getter reads Unity API (main-thread only), so there is
        /// nothing to offload to a background thread. Running synchronously also lets a MissingReferenceException
        /// from a destroyed component propagate to the caller (SemantizationCore.CheckForChanges), which closes
        /// the interval and stops observing it.
        /// </summary>
        public void CheckForChanges()
        {
            if (observedProperty == null || parentObject == null) return;

            object currentValue = observedProperty.Getter();
            if (currentValue == null) return;

            if (!HasValueChanged(currentValue, observedProperty.LastValue)) return;

            // limit the semantization with the graph instantPerSecond
            Instant currentInstant = GraphManager.CurrentInstant;
            if (lastSemantizedInstant != null && currentInstant == lastSemantizedInstant) return;

            lastSemantizedInstant = currentInstant;
            observedProperty.LastValue = currentValue;
            Semanticize(currentInstant);
        }

        private bool HasValueChanged(object currentValue, object lastValue)
        {
            if (currentValue is double || currentValue is float)
            {
                // Compare double values with a precision of 3 decimal places
                double roundedCurrent = Math.Round(Convert.ToDouble(currentValue), 3);
                double roundedLast = lastValue != null ? Math.Round(Convert.ToDouble(lastValue), 3) : double.NaN;
                return !Equals(roundedCurrent, roundedLast);
            }

            if (currentValue is Vector2 currentVector2 && lastValue is Vector2 lastVector2)
            {
                // Compare Vector2 values with a precision of 3 decimal places
                return !AreVectorsEqual(currentVector2, lastVector2);
            }

            if (currentValue is Vector3 currentVector3 && lastValue is Vector3 lastVector3)
            {
                // Compare Vector3 values with a precision of 3 decimal places
                return !AreVectorsEqual(currentVector3, lastVector3);
            }

            if (currentValue is Vector4 currentVector4 && lastValue is Vector4 lastVector4)
            {
                // Compare Vector4 values with a precision of 3 decimal places
                return !AreVectorsEqual(currentVector4, lastVector4);
            }

            // Compare other types of values directly
            return !Equals(currentValue, lastValue);
        }

        private bool AreVectorsEqual(Vector2 v1, Vector2 v2)
        {
            return Math.Round(v1.x, 3) == Math.Round(v2.x, 3) &&
                   Math.Round(v1.y, 3) == Math.Round(v2.y, 3);
        }

        private bool AreVectorsEqual(Vector3 v1, Vector3 v2)
        {
            return Math.Round(v1.x, 3) == Math.Round(v2.x, 3) &&
                   Math.Round(v1.y, 3) == Math.Round(v2.y, 3) &&
                   Math.Round(v1.z, 3) == Math.Round(v2.z, 3);
        }

        private bool AreVectorsEqual(Vector4 v1, Vector4 v2)
        {
            return Math.Round(v1.x, 3) == Math.Round(v2.x, 3) &&
                   Math.Round(v1.y, 3) == Math.Round(v2.y, 3) &&
                   Math.Round(v1.z, 3) == Math.Round(v2.z, 3) &&
                   Math.Round(v1.w, 3) == Math.Round(v2.w, 3);
        }

        /// <summary>
        /// Semantizes the property.
        /// </summary>
        public void Semanticize(Instant currentInstant)
        {
            if (SvenSettings.Debug)
                Debug.Log("Semantizing property (" + parentComponent.name + ")." + parentComponent.GetType().Name + "." + Name + " with value " + observedProperty.LastValue);
            DestroyUUID();

            IUriNode propertyNode = GraphManager.CreateUriNode(":" + GetUUID());
            string propertyTypeName = MapppedProperties.GetValue(observedProperty.LastValue.GetType()).TypeName;
            IUriNode propertyTypeNode = GraphManager.CreateUriNode(propertyTypeName.Contains(":") ? propertyTypeName : "sven:" + propertyTypeName);

            string propertyName = name.Contains(":") ? name : "sven:" + name;
            GraphManager.Assert(new Triple(ParentComponentNode, GraphManager.CreateUriNode(propertyName), propertyNode));
            GraphManager.Assert(new Triple(propertyNode, GraphManager.CreateUriNode("rdf:type"), propertyTypeNode));
            GraphManager.Assert(new Triple(propertyNode, GraphManager.CreateUriNode("sven:exactType"), propertyTypeNode));

            Interval oldInterval = interval;
            interval = new Interval();
            oldInterval?.End(currentInstant, interval);
            oldInterval?.Semanticize();

            interval.Start(currentInstant, oldInterval);
            IUriNode intervalNode = interval.Semanticize();
            GraphManager.Assert(new Triple(propertyNode, GraphManager.CreateUriNode("sven:hasTemporalExtent"), intervalNode));

            if (observedProperty.Callback != null)
            {
                observedProperty.Callback(propertyNode);
            }
            else
            {
                Dictionary<string, object> values = observedProperty.LastValue.GetSemantizableValues();
                foreach (KeyValuePair<string, object> value in values)
                {
                    string stringValue = value.Value.ToRdfString();
                    string XmlSchemaDataType = value.Value.GetXmlSchemaTypes();
                    if (XmlSchemaDataType == XmlSpecsHelper.XmlSchemaDataTypeBoolean) stringValue = stringValue.ToLower();
                    ILiteralNode literalNode = GraphManager.CreateLiteralNode(stringValue, new Uri(XmlSchemaDataType));
                    GraphManager.Assert(new Triple(propertyNode, GraphManager.CreateUriNode(value.Key.Contains(":") ? value.Key : "sven:" + value.Key), literalNode));

                    if (simplifiedName != "")
                    {
                        string simplifiedType = value.Key == "value" ? "" : value.Key.ToUpper();
                        Triple triple = new(ParentObjectNode, GraphManager.CreateUriNode("sven:" + simplifiedName + simplifiedType), literalNode);
                        GraphManager.Assert(triple);
                        GraphManager.Assert(new Triple(GraphManager.CreateTripleNode(triple), GraphManager.CreateUriNode("sven:hasTemporalExtent"), intervalNode));
                    }
                }

            }
        }

        public void Destroy()
        {
            interval?.End(GraphManager.CurrentInstant);
            interval?.Semanticize();
            DestroyUUID();
        }
    }
}
