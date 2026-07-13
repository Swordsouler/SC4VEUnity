// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;

namespace Sven.Content
{
    public interface ISemanticAnnotation
    {
        public static string SemanticTypeName => throw new NotImplementedException();
        public static Type GetType(string semanticTypeName)
        {
            // get all IComponentMapping implementations and find the one with the matching SemanticTypeName == semancTypeName
            var mappings = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(ISemanticAnnotation).IsAssignableFrom(type) && !type.IsInterface);
            foreach (var mapping in mappings)
            {
                // Get the SemanticTypeName property value
                var semanticTypeNameProperty = mapping.GetProperty("SemanticTypeName", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (semanticTypeNameProperty != null)
                {
                    var semanticTypeNameValue = semanticTypeNameProperty.GetValue(null) as string;
                    if (semanticTypeNameValue == semanticTypeName)
                    {
                        return mapping;
                    }
                }
            }
            throw new ArgumentException($"No ISemanticAnnotation found for semantic type name: {semanticTypeName}");
        }

        public static Type[] GetTypeHierarchy(Type leafType)
        {
            var hierarchy = new List<Type>();
            var currentType = leafType;
            while (currentType != null && typeof(ISemanticAnnotation).IsAssignableFrom(currentType))
            {
                hierarchy.Add(currentType);
                currentType = currentType.BaseType;
            }
            return hierarchy.ToArray();
        }

        public static string[] GetSemanticTypes(string semanticTypeName)
        {
            var leafType = GetType(semanticTypeName);
            var hierarchy = GetTypeHierarchy(leafType);
            return hierarchy
                .Select(t => t.GetProperty("SemanticTypeName", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetValue(null) as string)
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct()
                .ToArray();
        }

        private static string[] _availableAnnotationNames;

        public static string[] RefreshAvailableAnnotationTypes()
        {
            _availableAnnotationNames = null;
            return GetAvailableAnnotationTypes();
        }

        public static string[] GetAvailableAnnotationTypes()
        {
            if (_availableAnnotationNames != null)
            {
                return _availableAnnotationNames;
            }

            var componentTypes = new List<Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    componentTypes.AddRange(assembly.GetTypes().Where(type =>
                        typeof(ISemanticAnnotation).IsAssignableFrom(type) &&
                        !type.IsInterface));
                }
                catch (ReflectionTypeLoadException)
                {
                    // Ignorer les erreurs de chargement d'assembly
                }
            }

            var annotationNames = new List<string>();

            foreach (var type in componentTypes)
            {
                // Debug.Log($"Found ISemanticAnnotation type: {type.FullName}");
                try
                {
                    var prop = type.GetProperty("SemanticTypeName", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    if (prop != null)
                    {
                        var value = prop.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(value))
                        {
                            annotationNames.Add(value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Could not get SemanticTypeName for type {type.FullName}: {ex.Message}");
                }
            }

            _availableAnnotationNames = annotationNames.Distinct().OrderBy(n => n).ToArray();
            return _availableAnnotationNames;
        }




        private static List<string> _availableTypes;
        private static string _cachedLocale;

        public static async Task<List<string>> GetAvailableTypesAsync(string locale)
        {
            if (_availableTypes == null || _cachedLocale != locale)
            {
                _availableTypes = await GetAllAvailableTypes(locale);
                _cachedLocale = locale;
            }
            return _availableTypes;
        }

        public static async Task<List<string>> GetAllAvailableTypes(string locale)
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
    ?component rdfs:subClassOf sven:AnnotationComponent ;
               rdfs:label ?label .
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