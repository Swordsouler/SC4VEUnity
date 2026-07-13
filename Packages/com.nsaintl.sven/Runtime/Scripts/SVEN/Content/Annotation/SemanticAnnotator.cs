using Sven.Content;
using Sven.GraphManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VDS.RDF;

namespace Sven.Multimodality
{
    public class SemanticAnnotator : MonoBehaviour, IComponentMapping
    {
        [SerializeField]
        private List<string> _annotations = new();
        public List<string> Annotations
        {
            get => _annotations;
            set => _annotations = value;
        }

        public static ComponentMapping ComponentMapping()
        {
            return new("Annotator",
                new List<Delegate>
                {
                    (Func<SemanticAnnotator, ComponentProperty>)(annotator => new ComponentProperty("enabled", () => annotator.enabled, value => annotator.enabled = value.ToString() == "true", 1)),
                    (Func<SemanticAnnotator, ComponentProperty>)(annotator => new ComponentProperty("annotation", () => string.Join(",", annotator.Annotations.Select(a => a)),
                        value =>
                        {
                            string valueString = value.ToString();
                            if(annotator.Annotations.Contains(valueString)) return;
                            annotator.Annotations.Add(valueString);
                        }, 1,
                        propertyNode =>
                        {
                            foreach (var annotation in annotator.Annotations)
                                GraphManager.Assert(new Triple(propertyNode, GraphManager.CreateUriNode("sven:value"), GraphManager.CreateUriNode(annotation)));
                        }))
                });
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(SemanticAnnotator)), CanEditMultipleObjects]
    public class SemanticAnnotatorEditor : UnityEditor.Editor
    {
        private SerializedProperty _annotationsProperty;
        private string[] _availableAnnotationNames;
        private Dictionary<string, List<string>> _childToParents = new();
        private Dictionary<string, List<string>> _parentToChildren = new();

        private void OnEnable()
        {
            _annotationsProperty = serializedObject.FindProperty("_annotations");
            FindAvailableAnnotationTypes();
        }

        private void FindAvailableAnnotationTypes()
        {
            _availableAnnotationNames = ISemanticAnnotation.RefreshAvailableAnnotationTypes();
            BuildHierarchy();
        }

        private void BuildHierarchy()
        {
            _childToParents.Clear();
            _parentToChildren.Clear();

            if (_availableAnnotationNames == null) return;

            foreach (var name in _availableAnnotationNames)
            {
                var type = ISemanticAnnotation.GetType(name);
                if (type == null) continue;

                // Child to Parents
                var hierarchy = ISemanticAnnotation.GetTypeHierarchy(type);
                _childToParents[name] = hierarchy
                    .Select(t => t.GetProperty("SemanticTypeName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy)?.GetValue(null) as string)
                    .Where(n => !string.IsNullOrEmpty(n) && n != name)
                    .ToList();

                // Parent to Children
                if (type.BaseType != null && typeof(ISemanticAnnotation).IsAssignableFrom(type.BaseType))
                {
                    var baseNameProp = type.BaseType.GetProperty("SemanticTypeName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
                    if (baseNameProp != null)
                    {
                        var baseName = baseNameProp.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(baseName))
                        {
                            if (!_parentToChildren.ContainsKey(baseName))
                            {
                                _parentToChildren[baseName] = new List<string>();
                            }
                            _parentToChildren[baseName].Add(name);
                        }
                    }
                }
            }
        }

        private List<string> GetAllChildren(string parentName)
        {
            var allChildren = new List<string>();
            if (_parentToChildren.TryGetValue(parentName, out var directChildren))
            {
                foreach (var child in directChildren)
                {
                    allChildren.Add(child);
                    allChildren.AddRange(GetAllChildren(child));
                }
            }
            return allChildren;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (_availableAnnotationNames == null || _availableAnnotationNames.Length == 0)
            {
                EditorGUILayout.HelpBox("No semantic annotations available.", MessageType.Info);
                if (GUILayout.Button("Refresh annotations"))
                    FindAvailableAnnotationTypes();
                return;
            }

            var currentAnnotations = new HashSet<string>();
            for (int i = 0; i < _annotationsProperty.arraySize; i++)
            {
                currentAnnotations.Add(_annotationsProperty.GetArrayElementAtIndex(i).stringValue);
            }

            int mask = 0;
            for (int i = 0; i < _availableAnnotationNames.Length; i++)
            {
                if (currentAnnotations.Contains(_availableAnnotationNames[i]))
                {
                    mask |= (1 << i);
                }
            }

            EditorGUI.BeginChangeCheck();
            int newMask = EditorGUILayout.MaskField("Annotations", mask, _availableAnnotationNames);

            if (EditorGUI.EndChangeCheck())
            {
                int changedBits = mask ^ newMask;
                var finalSelectedAnnotations = new HashSet<string>(currentAnnotations);

                for (int i = 0; i < _availableAnnotationNames.Length; i++)
                {
                    if ((changedBits & (1 << i)) != 0)
                    {
                        string changedAnnotation = _availableAnnotationNames[i];
                        bool isSelected = (newMask & (1 << i)) != 0;

                        if (isSelected)
                        {
                            finalSelectedAnnotations.Add(changedAnnotation);
                            if (_childToParents.TryGetValue(changedAnnotation, out var parents))
                            {
                                foreach (var parent in parents)
                                {
                                    finalSelectedAnnotations.Add(parent);
                                }
                            }
                        }
                        else
                        {
                            finalSelectedAnnotations.Remove(changedAnnotation);
                            var children = GetAllChildren(changedAnnotation);
                            foreach (var child in children)
                            {
                                finalSelectedAnnotations.Remove(child);
                            }
                        }
                    }
                }

                // Apply the changes to all selected objects
                foreach (var t in targets)
                {
                    var annotator = (SemanticAnnotator)t;
                    annotator.Annotations.Clear();
                    annotator.Annotations.AddRange(finalSelectedAnnotations);
                    EditorUtility.SetDirty(annotator);
                }
            }

            if (GUILayout.Button("Refresh annotations"))
                FindAvailableAnnotationTypes();

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}