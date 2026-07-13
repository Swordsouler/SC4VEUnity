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
    /// Represents the grasp area in the scene.
    /// </summary>
    public class GraspArea : Interactor
    {
        /// <summary>
        /// The radius of the sphere.
        /// </summary>
        [field: SerializeField]
        public float GraspDistance { get; set; } = 10f;

        /// <summary>
        /// The height of the capsule. If 0, uses a sphere instead.
        /// </summary>
        [field: SerializeField]
        [field: Min(0f)]
        public float GraspHeight { get; set; } = 0f;

        protected override IEnumerator CheckInteractor(float i)
        {
            while (true)
            {
                Vector3 center = transform.position;
                float radius = GraspDistance;

                Collider[] colliders;
                int colliderCount;

                if (GraspHeight > 0f)
                {
                    // Calcule les points de début et de fin de la capsule
                    Vector3 point1 = center - transform.up * (GraspHeight * 0.5f);
                    Vector3 point2 = center + transform.up * (GraspHeight * 0.5f);
                    colliderCount = OverlapCapsuleInto(point1, point2, radius, out colliders);
                }
                else
                {
                    colliderCount = OverlapSphereInto(center, radius, Physics.AllLayers, out colliders);
                }

                HashSet<SemantizationCore> newVisibleObjects = new();

                for (int j = 0; j < colliderCount; j++)
                {
                    Collider collider = colliders[j];
                    if (collider.TryGetComponent(out SemantizationCore semantizationCore))
                    {
                        newVisibleObjects.Add(semantizationCore);
                        if (!currentInteractedObjects.Contains(semantizationCore))
                        {
                            // Object enters the sphere area, create interval for interaction and semantize the action
                            string dictionaryKey = $"{_semantizationCore.GetUUID()}-{semantizationCore.GetUUID()}";
                            // call start interval semantization of collisionevent
                            if (!_collisionEvents.ContainsKey(dictionaryKey))
                            {
                                if (SvenSettings.Debug) Debug.Log("Object " + semantizationCore.name + " enters the grasp area.");
                                CollisionEvent collisionEvent = new(_semantizationCore, semantizationCore);
                                collisionEvent.Start(GraphManager.CurrentInstant);
                                collisionEvent.Semanticize();
                                _collisionEvents.Add(dictionaryKey, collisionEvent);
                            }
                        }
                    }
                }

                // Detect objects that are no longer visible
                foreach (SemantizationCore obj in currentInteractedObjects)
                {
                    if (!newVisibleObjects.Contains(obj))
                    {
                        // Object exits the sphere area, close interval for interaction and semantize the action
                        // call end interval semantization of collisionevent
                        string dictionaryKey = $"{_semantizationCore.GetUUID()}-{obj.GetUUID()}";
                        if (_collisionEvents.TryGetValue(dictionaryKey, out CollisionEvent collisionEvent))
                        {
                            if (SvenSettings.Debug) Debug.Log("Object " + obj.name + " exits the grasp area.");
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
            Gizmos.color = SvenSettings.GraspAreaDebugColor;
            base.OnDrawGizmos();

            Vector3 center = transform.position;

            if (GraspHeight > 0f)
            {
                DrawWireCapsule(center, transform.up, GraspDistance, GraspHeight);
            }
            else
            {
                Gizmos.DrawWireSphere(center, GraspDistance);
            }
        }

        private void DrawWireCapsule(Vector3 center, Vector3 direction, float radius, float height)
        {
            Vector3 point1 = center - direction * (height * 0.5f);
            Vector3 point2 = center + direction * (height * 0.5f);

            // Calcul des vecteurs perpendiculaires
            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
            if (right.magnitude < 0.01f)
                right = Vector3.Cross(direction, Vector3.right).normalized;

            Vector3 forward = Vector3.Cross(right, direction).normalized;

            // Dessiner les cercles aux extrémités
            DrawCircle(point1, direction, radius, 24);
            DrawCircle(point2, direction, radius, 24);

            // Dessiner les 4 lignes de connexion principales
            Gizmos.DrawLine(point1 + right * radius, point2 + right * radius);
            Gizmos.DrawLine(point1 - right * radius, point2 - right * radius);
            Gizmos.DrawLine(point1 + forward * radius, point2 + forward * radius);
            Gizmos.DrawLine(point1 - forward * radius, point2 - forward * radius);

            // Dessiner les hémisphères
            int meridians = 8;
            int parallels = 4;

            for (int m = 0; m < meridians; m++)
            {
                float azimuth = (m / (float)meridians) * 360f * Mathf.Deg2Rad;
                Vector3 meridianDir = Mathf.Cos(azimuth) * right + Mathf.Sin(azimuth) * forward;

                Vector3 prevPoint1 = point1;
                Vector3 prevPoint2 = point2;

                for (int p = 1; p <= parallels; p++)
                {
                    float angle = (p / (float)parallels) * 90f * Mathf.Deg2Rad;
                    float parallelRadius = Mathf.Sin(angle) * radius;
                    float heightOffset = Mathf.Cos(angle) * radius;

                    // Points sur l'hémisphère inférieur
                    Vector3 currentPoint1 = point1 - direction * heightOffset + meridianDir * parallelRadius;
                    Gizmos.DrawLine(prevPoint1, currentPoint1);
                    prevPoint1 = currentPoint1;

                    // Points sur l'hémisphère supérieur
                    Vector3 currentPoint2 = point2 + direction * heightOffset + meridianDir * parallelRadius;
                    Gizmos.DrawLine(prevPoint2, currentPoint2);
                    prevPoint2 = currentPoint2;
                }
            }

            // Dessiner les parallèles sur les hémisphères
            for (int p = 1; p < parallels; p++)
            {
                float angle = (p / (float)parallels) * 90f * Mathf.Deg2Rad;
                float parallelRadius = Mathf.Sin(angle) * radius;
                float heightOffset = Mathf.Cos(angle) * radius;

                Vector3 parallelCenter1 = point1 - direction * heightOffset;
                Vector3 parallelCenter2 = point2 + direction * heightOffset;

                DrawCircle(parallelCenter1, direction, parallelRadius, 24);
                DrawCircle(parallelCenter2, direction, parallelRadius, 24);
            }
        }

        private void DrawWireHemisphere(Vector3 center, Vector3 direction, float radius)
        {
            Vector3 right = Vector3.Cross(direction, Vector3.forward).normalized;
            if (right.magnitude < 0.01f)
                right = Vector3.Cross(direction, Vector3.right).normalized;

            Vector3 forward = Vector3.Cross(right, direction).normalized;

            int segments = 16;
            for (int i = 0; i <= segments / 2; i++)
            {
                float angle1 = (i / (float)segments) * 180f * Mathf.Deg2Rad;
                float angle2 = ((i + 1) / (float)segments) * 180f * Mathf.Deg2Rad;

                for (int j = 0; j < segments; j++)
                {
                    float azimuth1 = (j / (float)segments) * 360f * Mathf.Deg2Rad;
                    float azimuth2 = ((j + 1) / (float)segments) * 360f * Mathf.Deg2Rad;

                    Vector3 p1 = center + direction * (Mathf.Cos(angle1) * radius) +
                                (Mathf.Cos(azimuth1) * right + Mathf.Sin(azimuth1) * forward) * (Mathf.Sin(angle1) * radius);
                    Vector3 p2 = center + direction * (Mathf.Cos(angle2) * radius) +
                                (Mathf.Cos(azimuth1) * right + Mathf.Sin(azimuth1) * forward) * (Mathf.Sin(angle2) * radius);

                    Gizmos.DrawLine(p1, p2);

                    Vector3 p3 = center + direction * (Mathf.Cos(angle1) * radius) +
                                (Mathf.Cos(azimuth2) * right + Mathf.Sin(azimuth2) * forward) * (Mathf.Sin(angle1) * radius);

                    Gizmos.DrawLine(p1, p3);
                }
            }
        }

        private void DrawCircle(Vector3 center, Vector3 normal, float radius, int segments)
        {
            Vector3 right = Vector3.Cross(normal, Vector3.forward).normalized;
            if (right.magnitude < 0.01f)
                right = Vector3.Cross(normal, Vector3.right).normalized;

            Vector3 forward = Vector3.Cross(right, normal).normalized;

            Vector3 previousPoint = center + right * radius;

            for (int i = 1; i <= segments; i++)
            {
                float angle = (i / (float)segments) * 360f * Mathf.Deg2Rad;
                Vector3 offset = (Mathf.Cos(angle) * right + Mathf.Sin(angle) * forward) * radius;
                Vector3 currentPoint = center + offset;

                Gizmos.DrawLine(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }
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
    sven:GraspAreaObject rdfs:label ?label .
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
    sven:GraspAreaObject sven:deicticWord ?label .
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