// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.GraphManagement;
using Sven.OwlTime;
using Sven.Utils;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;

namespace Sven.Content
{
    /// <summary>
    /// Core component to semantize content.
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("SVEN/Semantization Core")]
    public class SemantizationCore : MonoBehaviour
    {
        /// <summary>
        /// Components to semantize at initialization. WARNING: Do not use this list for algorithmic purposes (it's just for the Unity Editor).
        /// </summary>
        [HideInInspector]
        public List<SemanticComponent> componentsToSemanticize = new();

        /// <summary>
        /// Properties of the each Component to semantize.
        /// </summary>
        private readonly Dictionary<Component, SemanticComponent> componentsProperties = new();

        /// <summary>
        /// Coroutine to check for changes in the properties of the GameObject.
        /// </summary>
        private Coroutine _checkForChangesCoroutine;

        /// <summary>
        /// Start is called before the first frame update.
        /// </summary>
        private void Start()
        {
            // Master switch: when SVEN is disabled, this object is never observed/semantized.
            if (!SvenSettings.Enabled) return;
            Component component = GetComponent<Component>();
            componentsToSemanticize.RemoveAll(c => c == null || c.Component == null || !component.gameObject.Equals(c.Component.gameObject));
            InitializeAsync();
        }

        /// <summary>
        /// Initializes the semantization of the GameObject, his components and setup the properties observers.
        /// </summary>
        private async void InitializeAsync()
        {
            bool isGraphInitialized = false;
            for (int i = 0; i < 5; i++)
            {
                isGraphInitialized = GraphManager.IsGraphInitialized;
                if (isGraphInitialized)
                    break;
                else await Task.Delay(2000);
            }
            if (!isGraphInitialized)
            {
                Debug.LogError("GraphManager is not initialized. Please check your settings.");
                return;
            }
            try
            {
                // Semantize the GameObject attached to his properties and components
                componentsProperties.Add(this, new SemanticComponent
                {
                    Component = this,
                    ProcessingMode = SemanticProcessingMode.Dynamic,
                    Properties = SemanticObserve()
                });

                // foreach component in the GameObject, semantize the component and his properties
                foreach (var component in componentsToSemanticize)
                    componentsProperties.Add(component.Component, new SemanticComponent
                    {
                        Component = component.Component,
                        ProcessingMode = component.ProcessingMode,
                        Properties = component.Component.SemanticObserve()
                    });

                _checkForChangesCoroutine = StartCoroutine(LoopCheckForChanges(1.0f / SvenSettings.SemanticizeFrequency));
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
        }

        /// <summary>
        /// Adds a component to the semantization process on the fly. WARNING: When adding components, it will be semantized until his end of life.
        /// </summary>
        /// <param name="component">The component to add to the semantization process.</param>
        public void AddSemanticComponent(Component component, SemanticProcessingMode mode)
        {
            if (componentsToSemanticize.Find(c => c.Component == component) != null)
            {
                Debug.LogWarning("Component " + component.GetType().Name + " is already being semantized.");
                return;
            }

            componentsToSemanticize.Add(new SemanticComponent { Component = component, ProcessingMode = mode });
            List<Property> properties = component.SemanticObserve();
            componentsProperties.Add(component, new SemanticComponent
            {
                Component = component,
                ProcessingMode = mode,
                Properties = properties
            });
        }

        /// <summary>
        /// Overrides the default SemanticObserve method to focus on the GameObject semantization.
        /// </summary>
        /// <param name="graphBuffer">The graph buffer to semantize the GameObject.</param>
        public List<Property> SemanticObserve()
        {
            List<Property> properties = new()
            {
                new Property("name", () => gameObject.name),
                new Property("active", () => gameObject.activeSelf),
                new Property("tag", () => gameObject.tag),
                new Property("layer", () => LayerMask.LayerToName(gameObject.layer))
            };

            IUriNode gameObjectNode = GraphManager.CreateUriNode(":" + this.GetUUID());

            GraphManager.Assert(new Triple(gameObjectNode, GraphManager.CreateUriNode("rdf:type"), GraphManager.CreateUriNode("sven:VirtualObject")));
            GraphManager.Assert(new Triple(gameObjectNode, GraphManager.CreateUriNode("rdfs:label"), GraphManager.CreateLiteralNode(name)));
            foreach (Property property in properties)
            {
                property.SemanticObserve(this);
                if (SvenSettings.Debug)
                    Debug.Log("Observing property (" + name + ")." + GetType().Name + "." + property.Name);
            }

            Interval interval = this.GetInterval();
            interval.Start(GraphManager.CurrentInstant);
            IUriNode intervalNode = interval.Semanticize();
            GraphManager.Assert(new Triple(gameObjectNode, GraphManager.CreateUriNode("sven:hasTemporalExtent"), intervalNode));

            return properties;
        }

        #region Properties Observers

        /// <summary>
        /// Checks if the observed properties have changed and invokes the callbacks if they have.
        /// </summary>
        private void CheckForChanges()
        {
            List<Component> toRemove = new();
            foreach (KeyValuePair<Component, SemanticComponent> componentProperties in componentsProperties)
            {
                if (componentProperties.Value.IsSemantized && componentProperties.Value.ProcessingMode == SemanticProcessingMode.Static) continue;
                componentProperties.Value.IsSemantized = true;

                try
                {
                    foreach (Property property in componentProperties.Value.Properties)
                        property.CheckForChanges();
                }
                catch
                {
                    if (SvenSettings.Debug) Debug.LogWarning("Component " + componentProperties.Key.GetType().Name + " has been destroyed. Removing from semantization.");
                    toRemove.Add(componentProperties.Key);
                }
            }

            foreach (Component component in toRemove)
            {
                foreach (Property property in componentsProperties[component].Properties)
                    property.Destroy();

                Interval interval = component.GetInterval();
                interval.End(GraphManager.CurrentInstant);
                interval.Semanticize();
                component.DestroyUUID();

                componentsProperties.Remove(component);
            }
        }

        /// <summary>
        /// Coroutine to check for changes in the properties of the GameObject.
        /// </summary>
        private IEnumerator LoopCheckForChanges(float interval)
        {
            while (componentsProperties.Count > 0)
            {
                float startTime = Time.realtimeSinceStartup;
                CheckForChanges();
                float elapsedTime = Time.realtimeSinceStartup - startTime;

                float timeToWait = interval - elapsedTime;

                if (timeToWait > 0)
                {
                    yield return new WaitForSeconds(timeToWait);
                }
                else
                {
                    // We are already behind schedule, yield to the next frame to avoid blocking.
                    yield return null;
                }
            }
        }

        /// <summary>
        /// OnDisable is called when the behaviour becomes disabled or inactive.
        /// </summary>
        private void OnDisable()
        {
            CheckForChanges();
            if (_checkForChangesCoroutine != null) StopCoroutine(_checkForChangesCoroutine);
        }

        /// <summary>
        /// OnEnable is called when the object becomes enabled and active.
        /// </summary>
        private void OnEnable()
        {
            if (!SvenSettings.Enabled) return;
            if (_checkForChangesCoroutine != null) StopCoroutine(_checkForChangesCoroutine);
            StartCoroutine(LoopCheckForChanges(1.0f / SvenSettings.SemanticizeFrequency));
        }

        /// <summary>
        /// This function is called when the MonoBehaviour will be destroyed.
        /// </summary>
        public void OnDestroy()
        {
            foreach (KeyValuePair<Component, SemanticComponent> componentProperties in componentsProperties)
            {
                foreach (Property property in componentProperties.Value.Properties)
                    property.Destroy();

                Interval interval = componentProperties.Key.GetInterval();
                interval.End(GraphManager.CurrentInstant);
                interval.Semanticize();
                componentProperties.Key.DestroyUUID();
            }
        }

        #endregion
    }
}