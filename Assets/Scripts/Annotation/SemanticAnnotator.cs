using Sven.Content;
using Sven.GraphManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VDS.RDF;

namespace Sven.Multimodality
{
    public class SemanticAnnotator : MonoBehaviour, IComponentMapping
    {
        public static string SemanticTypeName => "sven:Annotator";

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
    [CustomEditor(typeof(SemanticAnnotator))]
    [CanEditMultipleObjects]
    public class SemanticAnnotatorEditor : UnityEditor.Editor
    {
        private SerializedProperty _annotationsProperty;
        private string[] _availableAnnotationNames;

        private void OnEnable()
        {
            _annotationsProperty = serializedObject.FindProperty("_annotations");
            FindAvailableAnnotationTypes();
        }

        private void FindAvailableAnnotationTypes()
        {
            var componentTypes = new List<Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    componentTypes.AddRange(assembly.GetTypes().Where(type =>
                        typeof(IComponentMapping).IsAssignableFrom(type) &&
                        !type.IsInterface &&
                        !type.IsAbstract &&
                        type != typeof(SemanticAnnotator)));
                }
                catch (System.Reflection.ReflectionTypeLoadException)
                {
                    // Ignorer les erreurs de chargement d'assembly
                }
            }

            var annotationNames = new List<string>();

            foreach (var type in componentTypes)
            {
                try
                {
                    // Accéder à la propriété statique directement sur le type
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
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Annotations", EditorStyles.boldLabel);

            int mask = 0;
            var selectedAnnotations = new List<string>();

            // Construire le masque et la liste des annotations sélectionnées
            // en se basant sur le premier objet sélectionné pour l'affichage initial.
            if (_annotationsProperty.arraySize > 0)
            {
                for (int i = 0; i < _annotationsProperty.arraySize; i++)
                {
                    selectedAnnotations.Add(_annotationsProperty.GetArrayElementAtIndex(i).stringValue);
                }
            }

            for (int i = 0; i < _availableAnnotationNames.Length; i++)
            {
                if (selectedAnnotations.Contains(_availableAnnotationNames[i]))
                {
                    mask |= (1 << i);
                }
            }

            // Afficher le champ de masque
            int newMask = EditorGUILayout.MaskField(mask, _availableAnnotationNames);

            // Si le masque a changé, mettre à jour la liste des annotations
            if (newMask != mask)
            {
                var newSelectedAnnotations = new List<string>();
                for (int i = 0; i < _availableAnnotationNames.Length; i++)
                {
                    if ((newMask & (1 << i)) != 0)
                    {
                        newSelectedAnnotations.Add(_availableAnnotationNames[i]);
                    }
                }

                // Appliquer les changements à tous les objets sélectionnés
                foreach (var t in targets)
                {
                    var annotator = (SemanticAnnotator)t;
                    annotator.Annotations.Clear();
                    annotator.Annotations.AddRange(newSelectedAnnotations);
                    EditorUtility.SetDirty(annotator);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}