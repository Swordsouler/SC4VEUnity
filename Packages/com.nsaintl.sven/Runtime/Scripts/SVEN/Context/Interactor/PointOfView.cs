// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.Content;
using Sven.GraphManagement;
using Sven.Utils;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;

namespace Sven.Context
{
    /// <summary>
    /// Represents the point of view in the scene.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PointOfView : Interactor
    {
        /// <summary>
        /// The camera component attached to this GameObject.
        /// </summary>
        public Camera cameraComponent;

        /// <summary>
        /// Called when the script instance is being loaded.
        /// </summary>
        protected new void Awake()
        {
            base.Awake();
            cameraComponent = GetComponent<Camera>();
            if (cameraComponent == null) Destroy(this);
        }

        /// <summary>
        /// Checks the field of view for SemantizationCore objects and updates the visible objects list.
        /// </summary>
        protected override IEnumerator CheckInteractor(float i)
        {
            while (true)
            {
                Vector3 cameraPosition = cameraComponent.transform.position;
                float visionDistance = cameraComponent.farClipPlane;

                int colliderCount = OverlapSphereInto(cameraPosition, visionDistance, cameraComponent.cullingMask, out Collider[] colliders);
                HashSet<SemantizationCore> newVisibleObjects = new();

                Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cameraComponent);

                for (int j = 0; j < colliderCount; j++)
                {
                    Collider collider = colliders[j];
                    if (GeometryUtility.TestPlanesAABB(frustumPlanes, collider.bounds))
                    {
                        if (collider.TryGetComponent(out SemantizationCore semantizationCore))
                        {
                            newVisibleObjects.Add(semantizationCore);
                            if (!currentInteractedObjects.Contains(semantizationCore))
                            {
                                // Object enters the field of view, create interval for interaction and semantize the action
                                string dictionaryKey = $"{_semantizationCore.GetUUID()}-{semantizationCore.GetUUID()}";
                                // call start interval semantization of collisionevent
                                if (!_collisionEvents.ContainsKey(dictionaryKey))
                                {
                                    if (SvenSettings.Debug) Debug.Log("Object " + semantizationCore.name + " enters the field of view.");
                                    CollisionEvent collisionEvent = new(_semantizationCore, semantizationCore);
                                    collisionEvent.Start(GraphManager.CurrentInstant);
                                    collisionEvent.Semanticize();
                                    _collisionEvents.Add(dictionaryKey, collisionEvent);
                                }
                            }
                        }
                    }
                }

                // Detect objects that are no longer visible
                foreach (SemantizationCore obj in currentInteractedObjects)
                {
                    if (!newVisibleObjects.Contains(obj))
                    {
                        // Object exits the field of view, close interval for interaction and semantize the action
                        // call end interval semantization of collisionevent
                        string dictionaryKey = $"{_semantizationCore.GetUUID()}-{obj.GetUUID()}";
                        if (_collisionEvents.TryGetValue(dictionaryKey, out CollisionEvent collisionEvent))
                        {
                            if (SvenSettings.Debug) Debug.Log("Object " + obj.name + " exits the field of view.");
                            collisionEvent.End(GraphManager.CurrentInstant);
                            collisionEvent.Semanticize();
                            _collisionEvents.Remove(dictionaryKey);
                        }
                    }
                }

                // Update the list of currently visible objects
                currentInteractedObjects = newVisibleObjects;
                yield return new WaitForSeconds(i);
            }
        }

        protected new void OnDrawGizmos()
        {
            if (!_debug) return;
            Gizmos.color = SvenSettings.PointOfViewDebugColor;
            base.OnDrawGizmos();

            if (cameraComponent == null) return;

            // Get the corners of the frustum
            Vector3[] frustumCorners = new Vector3[4];
            cameraComponent.CalculateFrustumCorners(new Rect(0, 0, 1, 1), cameraComponent.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);

            // Convert the corners to world space
            for (int i = 0; i < frustumCorners.Length; i++)
            {
                frustumCorners[i] = cameraComponent.transform.TransformPoint(frustumCorners[i]);
            }

            // Draw the frustum
            Gizmos.DrawLine(cameraComponent.transform.position, frustumCorners[0]);
            Gizmos.DrawLine(cameraComponent.transform.position, frustumCorners[1]);
            Gizmos.DrawLine(cameraComponent.transform.position, frustumCorners[2]);
            Gizmos.DrawLine(cameraComponent.transform.position, frustumCorners[3]);

            Gizmos.DrawLine(frustumCorners[0], frustumCorners[1]);
            Gizmos.DrawLine(frustumCorners[1], frustumCorners[2]);
            Gizmos.DrawLine(frustumCorners[2], frustumCorners[3]);
            Gizmos.DrawLine(frustumCorners[3], frustumCorners[0]);
        }

        private static List<string> _availableNames;
        private static List<string> _availableDeictics;
        private static string _cachedLocale;

        public static async Task<List<string>> GetAvailableNamesAsync(string locale)
        {
            if (_availableNames == null || _cachedLocale != locale)
            {
                _availableNames = await GetAllAvailableNames(locale);
                _cachedLocale = locale;
            }
            return _availableNames;
        }

        public static async Task<List<string>> GetAllAvailableNames(string locale)
        {
            // load a graph with colors from resources
            Graph graph = new();
            // load ontology like GraphManager
            Dictionary<string, string> ontologies = await SvenSettings.GetOntologiesAsync();
            TurtleParser turtleParser = new();
            foreach (KeyValuePair<string, string> ontology in ontologies)
            {
                turtleParser.Load(graph, ontology.Value);
            }

            string query = $@"
PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

SELECT ?label
WHERE {{
    sven:PointOfView rdfs:label ?label .
    FILTER(langMatches(lang(?label), ""{locale}""))
}}";

            if (graph.ExecuteQuery(query) is SparqlResultSet results)
            {
                return results.Select(result => (result["label"] as ILiteralNode)?.Value).Where(label => label != null).ToList();
            }

            return new List<string>();
        }

        public static async Task<List<string>> GetAvailableDeicticsAsync(string locale)
        {
            if (_availableDeictics == null || _cachedLocale != locale)
            {
                _availableDeictics = await GetAllAvailableDeictics(locale);
                _cachedLocale = locale;
            }
            return _availableDeictics;
        }

        public static async Task<List<string>> GetAllAvailableDeictics(string locale)
        {
            // load a graph with colors from resources
            Graph graph = new();
            // load ontology like GraphManager
            Dictionary<string, string> ontologies = await SvenSettings.GetOntologiesAsync();
            TurtleParser turtleParser = new();
            foreach (KeyValuePair<string, string> ontology in ontologies)
            {
                turtleParser.Load(graph, ontology.Value);
            }
            string query = $@"
PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
SELECT ?label
WHERE {{
    sven:PointOfView sven:deicticWord ?label .
    FILTER(langMatches(lang(?label), ""{locale}""))
}}";
            if (graph.ExecuteQuery(query) is SparqlResultSet results)
            {
                return results.Select(result => (result["label"] as ILiteralNode)?.Value).Where(label => label != null).ToList();
            }
            return new List<string>();
        }
    }
}