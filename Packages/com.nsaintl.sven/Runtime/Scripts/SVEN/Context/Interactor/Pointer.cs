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
    /// Represents the pointer in the scene.
    /// </summary>
    public class Pointer : Interactor
    {
        /// <summary>
        /// The maximum distance for the pointer.
        /// </summary>
        [field: SerializeField]
        public int PointerIndex { get; set; } = 0;
        [field: SerializeField]
        public float PointerDistance { get; set; } = 20f;
        [field: SerializeField]
        [field: Range(0f, 89f)]
        public float PointerConeAngle { get; set; } = 0f;
        [field: SerializeField]
        public Vector3 PointerPosition { get; set; } = Vector3.zero;
        [field: SerializeField]
        public Vector3 PointerDirection { get; set; } = Vector3.forward;
        [field: SerializeField]
        public Vector3 PointerHitPosition { get; set; } = Vector3.zero;
        [field: SerializeField]
        public float PointerHitDistance { get; set; } = 0f;

        // Constante pour harmoniser la géométrie du cône
        private const int CONE_SEGMENTS = 12;
        private const int CONE_RINGS = 3;

        protected override IEnumerator CheckInteractor(float i)
        {
            while (true)
            {
                PointerPosition = transform.position;
                PointerDirection = transform.forward;
                float visionDistance = PointerDistance;

                Ray ray = new(PointerPosition, PointerDirection);
                RaycastHit[] hits;
                int hitCount;

                if (PointerConeAngle > 0f)
                {
                    hits = GetConeCastHits(PointerPosition, PointerDirection, PointerDistance, PointerConeAngle);
                    hitCount = hits.Length;
                }
                else
                {
                    hitCount = RaycastAllInto(ray, visionDistance, out hits);
                }

                // Détermine le point le plus proche touché par le rayon ; si aucun hit, prend le point à la distance maximale du pointer
                if (hitCount > 0)
                {
                    float minDist = float.MaxValue;
                    Vector3 closestPoint = PointerPosition + PointerDirection.normalized * visionDistance;
                    for (int k = 0; k < hitCount; k++)
                    {
                        RaycastHit h = hits[k];
                        if (h.distance < minDist)
                        {
                            minDist = h.distance;
                            closestPoint = h.point;
                        }
                    }
                    PointerHitPosition = closestPoint;
                    PointerHitDistance = minDist;
                }
                else
                {
                    PointerHitPosition = PointerPosition + PointerDirection.normalized * visionDistance;
                    PointerHitDistance = visionDistance;
                }

                HashSet<SemantizationCore> newVisibleObjects = new();

                for (int j = 0; j < hitCount; j++)
                {
                    RaycastHit hit = hits[j];
                    Collider collider = hit.collider;
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
                                if (SvenSettings.Debug) Debug.Log("Object " + semantizationCore.name + " enters the pointer range.");
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
                        // Object exits the field of view, close interval for interaction and semantize the action
                        // call end interval semantization of collisionevent
                        string dictionaryKey = $"{_semantizationCore.GetUUID()}-{obj.GetUUID()}";
                        if (_collisionEvents.TryGetValue(dictionaryKey, out CollisionEvent collisionEvent))
                        {
                            if (SvenSettings.Debug) Debug.Log("Object " + obj.name + " exits the pointer range.");
                            collisionEvent.End(GraphManager.CurrentInstant);
                            collisionEvent.Semanticize();
                            _collisionEvents.Remove(dictionaryKey);
                        }
                    }
                }
                // sort the hashset by distance to the pointer
                List<SemantizationCore> sortedVisibleObjects = new(newVisibleObjects);
                sortedVisibleObjects.Sort((a, b) => Vector3.Distance(a.transform.position, PointerPosition).CompareTo(Vector3.Distance(b.transform.position, PointerPosition)));
                // Update the list of currently interacted objects
                currentInteractedObjects.Clear();
                currentInteractedObjects.UnionWith(sortedVisibleObjects);

                yield return new WaitForSeconds(i);
            }
        }

        private RaycastHit[] GetConeCastHits(Vector3 origin, Vector3 direction, float distance, float coneAngleDegrees)
        {
            Dictionary<Collider, RaycastHit> uniqueHits = new();
            Vector3 coneDirection = direction.normalized;
            float coneAngleRad = coneAngleDegrees * Mathf.Deg2Rad;

            // 1) Rayons d'échantillonnage
            CastRayAndAddHits(new Ray(origin, coneDirection), distance, coneAngleRad, origin, coneDirection, uniqueHits);

            Vector3 right = Vector3.Cross(coneDirection, Vector3.up).normalized;
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.Cross(coneDirection, Vector3.right).normalized;

            Vector3 up = Vector3.Cross(right, coneDirection).normalized;

            int baseSegments = Mathf.Max(CONE_SEGMENTS, Mathf.CeilToInt(coneAngleDegrees * 0.75f));

            for (int ring = 1; ring <= CONE_RINGS + 1; ring++)
            {
                float t = ring / (float)(CONE_RINGS + 1);
                float ringDistance = distance * t;
                float ringRadius = Mathf.Tan(coneAngleRad) * ringDistance;
                int segments = baseSegments * ring;

                for (int s = 0; s < segments; s++)
                {
                    float a = (s / (float)segments) * Mathf.PI * 2f;
                    Vector3 radial = (Mathf.Cos(a) * right + Mathf.Sin(a) * up) * ringRadius;
                    Vector3 samplePoint = origin + coneDirection * ringDistance + radial;
                    Vector3 rayDir = (samplePoint - origin).normalized;

                    CastRayAndAddHits(new Ray(origin, rayDir), distance, coneAngleRad, origin, coneDirection, uniqueHits);
                }
            }

            // 2) Fallback volumétrique pour éviter les objets "entre deux rayons"
            AddConeOverlapFallbackHits(origin, coneDirection, distance, coneAngleRad, uniqueHits);

            return uniqueHits.Values.ToArray();
        }

        private void AddConeOverlapFallbackHits(
            Vector3 origin,
            Vector3 coneDirection,
            float distance,
            float coneAngleRad,
            Dictionary<Collider, RaycastHit> uniqueHits)
        {
            float coneRadius = Mathf.Tan(coneAngleRad) * distance;
            float sphereRadius = Mathf.Sqrt(distance * distance + coneRadius * coneRadius);
            Vector3 sphereCenter = origin + coneDirection * (distance * 0.5f);

            int colliderCount = OverlapSphereInto(sphereCenter, sphereRadius, Physics.AllLayers, out Collider[] colliders);
            Vector3 coneEnd = origin + coneDirection * distance;

            for (int c = 0; c < colliderCount; c++)
            {
                Collider collider = colliders[c];
                if (uniqueHits.ContainsKey(collider))
                    continue;

                Vector3 axisPoint = ClosestPointOnSegment(origin, coneEnd, collider.bounds.center);
                Vector3 closest = collider.ClosestPoint(axisPoint);

                if (!IsPointInCone(origin, coneDirection, closest, coneAngleRad, distance))
                    continue;

                Vector3 toClosest = closest - origin;
                if (toClosest.sqrMagnitude < 0.0001f)
                    continue;

                Ray ray = new(origin, toClosest.normalized);
                int hitCount = RaycastAllInto(ray, distance, out RaycastHit[] hits);

                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = hits[i];
                    if (hit.collider == collider && IsPointInCone(origin, coneDirection, hit.point, coneAngleRad, distance))
                    {
                        uniqueHits.Add(collider, hit);
                        break;
                    }
                }
            }
        }

        private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float abSqr = ab.sqrMagnitude;
            if (abSqr < 0.0001f)
                return a;

            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / abSqr);
            return a + ab * t;
        }

        protected new void OnDrawGizmos()
        {
            if (!_debug) return;
            Gizmos.color = SvenSettings.PointerDebugColor;
            base.OnDrawGizmos();

            Vector3 direction = transform.forward;
            Vector3 origin = transform.position;
            Vector3 destination = origin + direction * PointerDistance;

            if (PointerConeAngle > 0f)
            {
                DrawCone(origin, direction, PointerDistance, PointerConeAngle);
            }
            else
            {
                Gizmos.DrawLine(origin, destination);
            }
        }

        private void DrawCone(Vector3 origin, Vector3 direction, float distance, float coneAngleDegrees)
        {
            float coneAngleRad = coneAngleDegrees * Mathf.Deg2Rad;

            // Calcul des vecteurs perpendiculaires
            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
            if (right.magnitude < 0.01f)
                right = Vector3.Cross(direction, Vector3.right).normalized;

            Vector3 up = Vector3.Cross(right, direction).normalized;

            // Ligne centrale
            Vector3 destination = origin + direction.normalized * distance;
            Gizmos.DrawLine(origin, destination);

            // Dessiner les anneaux du cône
            for (int ring = 1; ring <= CONE_RINGS; ring++)
            {
                float ringDistance = distance * (ring / (float)(CONE_RINGS + 1));
                float ringRadius = Mathf.Tan(coneAngleRad) * ringDistance;
                Vector3 ringCenter = origin + direction.normalized * ringDistance;

                DrawCircle(ringCenter, direction, ringRadius, CONE_SEGMENTS);
            }

            // Dessiner le cercle de la base du cône
            float baseRadius = Mathf.Tan(coneAngleRad) * distance;
            DrawCircle(destination, direction, baseRadius, CONE_SEGMENTS);

            // Dessiner les lignes depuis l'origine vers les points du cercle de base
            for (int i = 0; i < CONE_SEGMENTS; i++)
            {
                float angle = (i / (float)CONE_SEGMENTS) * 360f * Mathf.Deg2Rad;
                Vector3 offset = (Mathf.Cos(angle) * right + Mathf.Sin(angle) * up) * baseRadius;
                Vector3 pointOnCircle = destination + offset;
                Gizmos.DrawLine(origin, pointOnCircle);
            }
        }

        private void DrawCircle(Vector3 center, Vector3 normal, float radius, int segments)
        {
            Vector3 right = Vector3.Cross(normal, Vector3.up).normalized;
            if (right.magnitude < 0.01f)
                right = Vector3.Cross(normal, Vector3.right).normalized;

            Vector3 up = Vector3.Cross(right, normal).normalized;

            Vector3 previousPoint = center + right * radius;

            for (int i = 1; i <= segments; i++)
            {
                float angle = (i / (float)segments) * 360f * Mathf.Deg2Rad;
                Vector3 offset = (Mathf.Cos(angle) * right + Mathf.Sin(angle) * up) * radius;
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
    sven:Pointer rdfs:label ?label .
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
    sven:Pointer sven:deicticWord ?label .
    FILTER(langMatches(lang(?label), ""{locale}""))
}}";
            if (graph.ExecuteQuery(query) is SparqlResultSet results)
            {
                return results.Select(result => (result["label"] as ILiteralNode)?.Value).Where(label => label != null).ToList();
            }
            return new List<string>();
        }

        private void CastRayAndAddHits(
            Ray ray,
            float distance,
            float coneAngleRad,
            Vector3 origin,
            Vector3 direction,
            Dictionary<Collider, RaycastHit> uniqueHits)
        {
            int count = RaycastAllInto(ray, distance, out RaycastHit[] hits);

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (IsPointInCone(origin, direction, hit.point, coneAngleRad, distance))
                {
                    if (!uniqueHits.ContainsKey(hit.collider))
                        uniqueHits.Add(hit.collider, hit);
                }
            }
        }

        private bool IsPointInCone(
            Vector3 coneOrigin,
            Vector3 coneDirection,
            Vector3 point,
            float coneAngleRad,
            float maxDistance)
        {
            Vector3 v = point - coneOrigin;
            float sqrDist = v.sqrMagnitude;

            if (sqrDist > maxDistance * maxDistance)
                return false;

            if (sqrDist < 0.0001f)
                return true;

            float cosThreshold = Mathf.Cos(coneAngleRad);
            float dot = Vector3.Dot(coneDirection.normalized, v.normalized);

            return dot >= cosThreshold;
        }
    }
}