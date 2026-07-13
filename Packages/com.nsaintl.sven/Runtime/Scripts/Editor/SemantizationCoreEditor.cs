// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if UNITY_EDITOR
using NaughtyAttributes.Editor;
using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sven.Editor
{
    /// <summary>
    /// Custom editor for the SemantizationCore component.
    /// </summary>
    [CustomEditor(typeof(SemantizationCore))]
    [CanEditMultipleObjects]
    public class SemantizationCoreEditor : NaughtyInspector
    {
        /// <summary>
        /// Flag to show or hide the semantization foldout.
        /// </summary>
        private bool showSemantization = true;

        /// <summary>
        /// Override the default inspector GUI to display the semantization foldout.
        /// </summary>
        public override void OnInspectorGUI()
        {
            // Display default inspector elements with NaughtyAttributes
            base.OnInspectorGUI();

            // Display the foldout for semantization
            showSemantization = EditorGUILayout.Foldout(showSemantization, "Semantization", true);
            if (showSemantization)
            {
                // Get the serialized property for componentsToSemantize
                SerializedProperty componentsToSemantizeProperty = serializedObject.FindProperty("componentsToSemantize");

                // Dictionary to group components by type
                Dictionary<Type, List<Component>> groupedComponents = new Dictionary<Type, List<Component>>();

                // Get all components of the GameObject and group them by type
                foreach (UnityEngine.Object targetObject in serializedObject.targetObjects)
                {
                    SemantizationCore core = (SemantizationCore)targetObject;
                    Component[] allComponents = core.GetComponents<Component>();

                    foreach (Component component in allComponents)
                    {
                        if (component is SemantizationCore) continue;

                        Type componentType = component.GetType();
                        if (!groupedComponents.ContainsKey(componentType))
                        {
                            groupedComponents[componentType] = new List<Component>();
                        }
                        groupedComponents[componentType].Add(component);
                    }
                }

                // Disable GUI if the editor is in play mode
                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

                // Get the names of the SemanticProcessingMode enum values
                string[] processingModeOptions = Enum.GetNames(typeof(SemanticProcessingMode));

                // Display checkboxes and dropdowns for each group of components
                foreach (var group in groupedComponents)
                {
                    Type componentType = group.Key;
                    List<Component> components = group.Value;

                    // Check if the component has the Semantize method
                    bool hasGetProperties = MapppedComponents.ContainsKey(componentType);
                    bool isHidden = MapppedComponents.ContainsKey(componentType) && MapppedComponents.GetValue(componentType) == null;

                    if (isHidden) continue;

                    string label = componentType.Name;

                    // Create a GUIStyle for the label
                    GUIStyle labelStyle = new(EditorStyles.label);
                    if (!hasGetProperties)
                    {
                        labelStyle.normal.textColor = Color.yellow;
                        label = $"{label} (All properties will be semantized and not be replayed)";
                    }
                    else
                    {
                        string rdfType = MapppedComponents.GetValue(componentType).TypeName;
                        if (rdfType != null && rdfType != label)
                            label = $"{label} ({MapppedComponents.GetValue(componentType).TypeName})";
                    }

                    // Begin horizontal layout
                    EditorGUILayout.BeginHorizontal();

                    // Display a checkbox for the component with "(ALL)" and a tooltip if it doesn't have GetProperties
                    GUIContent content = new("", "The component will be fully semantized, which may cause performance issues. To fix this, declare a GetProperties function for this component in SemantizationExtensions.");
                    bool newIsSemantized = EditorGUILayout.Toggle(components.Exists(c => ((SemantizationCore)c.gameObject.GetComponent<SemantizationCore>()).componentsToSemanticize.Exists(sc => sc.Component == c)), GUILayout.Width(20));

                    // Disable the dropdown if the checkbox is not checked
                    EditorGUI.BeginDisabledGroup(!newIsSemantized);

                    // Display a dropdown for the component with the options from SemanticProcessingMode
                    int selectedIndex = EditorGUILayout.Popup(newIsSemantized ? ((SemantizationCore)components[0].gameObject.GetComponent<SemantizationCore>()).componentsToSemanticize.Find(sc => sc.Component == components[0])?.ProcessingMode == SemanticProcessingMode.Static ? 1 : 0 : 0, newIsSemantized ? processingModeOptions : new string[] { "" }, GUILayout.Width(80));

                    // End disabled group for dropdown
                    EditorGUI.EndDisabledGroup();

                    // Display the label
                    GUILayout.Label(label, labelStyle);

                    // End horizontal layout
                    EditorGUILayout.EndHorizontal();

                    // Update the componentsToSemantize list based on the checkbox state
                    foreach (Component component in components)
                    {
                        SemantizationCore core = (SemantizationCore)component.gameObject.GetComponent<SemantizationCore>();
                        bool isSemantized = core.componentsToSemanticize.Find(c => c.Component == component) != null;

                        if (newIsSemantized && !isSemantized)
                        {
                            core.componentsToSemanticize.Add(new SemanticComponent { Component = component, ProcessingMode = (SemanticProcessingMode)selectedIndex });
                            core.componentsToSemanticize.RemoveAll(c => c == null || c.Component == null || !component.gameObject.Equals(c.Component.gameObject));
                        }
                        else if (!newIsSemantized && isSemantized)
                        {
                            core.componentsToSemanticize.Remove(core.componentsToSemanticize.Find(c => c.Component == component));
                            core.componentsToSemanticize.RemoveAll(c => c == null || c.Component == null || !component.gameObject.Equals(c.Component.gameObject));
                        }

                        // Handle the dropdown selection (you can add your logic here)
                        if (newIsSemantized)
                        {
                            core.componentsToSemanticize.Find(c => c.Component == component).ProcessingMode = (SemanticProcessingMode)selectedIndex;
                        }
                    }
                }

                // Re-enable GUI
                EditorGUI.EndDisabledGroup();

                // Apply changes
                if (GUI.changed)
                {
                    serializedObject.ApplyModifiedProperties();
                    foreach (UnityEngine.Object targetObject in serializedObject.targetObjects)
                    {
                        EditorUtility.SetDirty(targetObject);
                    }
                }
            }
        }
    }
}
#endif