// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if UNITY_EDITOR
using Sven.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sven.Editor
{

    /// <summary>
    /// Helper window to manage SVEN more easily.
    /// </summary>
    public class SvenSettingsWindow : EditorWindow
    {
        private readonly Dictionary<string, Vector2> ontologiesScrollPosition = new();
        private readonly Dictionary<string, bool> ontologiesIsShown = new();

        [MenuItem("Window/SVEN Settings")]
        public static void ShowWindow()
        {
            GetWindow<SvenSettingsWindow>("SVEN Settings");
        }

        private void OnValidate()
        {
            SvenSettings.RefreshConfig();
        }

        private void OnLostFocus()
        {
            ontologiesIsShown.Clear();
            ontologiesScrollPosition.Clear();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("SVEN Settings", EditorStyles.boldLabel);

            bool refresh = false;

            bool enabled = SvenSettings.Enabled;
            bool newEnabled = EditorGUILayout.Toggle("Enable SVEN (semantization)", enabled);
            if (newEnabled != enabled)
            {
                SvenSettings.Enabled = newEnabled;
                EditorPrefs.SetBool(SvenSettings._enabledKey, newEnabled);
                refresh = true;
            }
            if (!newEnabled)
                EditorGUILayout.HelpBox("SVEN is disabled: the scene will not be semantized (no recording, no endpoint upload). Replay scenes are unaffected.", MessageType.Warning);

            bool useInside = SvenSettings.UseInside;
            bool newUseInside = EditorGUILayout.Toggle("Use inside", useInside);

            if (newUseInside != useInside)
            {
                EditorPrefs.SetBool(SvenSettings._useInsideKey, newUseInside);
                refresh = true;
            }

            bool debug = SvenSettings.Debug;
            bool newDebug = EditorGUILayout.Toggle("Show debug logs", debug);

            if (newDebug != debug)
            {
                EditorPrefs.SetBool(SvenSettings._debugKey, newDebug);
                refresh = true;
            }

            Color pointOfViewDebugColor = SvenSettings.PointOfViewDebugColor;
            Color newPointOfViewDebugColor = EditorGUILayout.ColorField("Point of View Debug Color", pointOfViewDebugColor);

            if (newPointOfViewDebugColor != pointOfViewDebugColor)
            {
                EditorPrefs.SetString(SvenSettings._pointOfViewDebugColorKey, ColorUtility.ToHtmlStringRGB(newPointOfViewDebugColor));
                refresh = true;
            }

            Color pointerDebugColor = SvenSettings.PointerDebugColor;
            Color newPointerDebugColor = EditorGUILayout.ColorField("Pointer Debug Color", pointerDebugColor);

            if (newPointerDebugColor != pointerDebugColor)
            {
                EditorPrefs.SetString(SvenSettings._pointerDebugColorKey, ColorUtility.ToHtmlStringRGB(newPointerDebugColor));
                refresh = true;
            }

            Color graspAreaDebugColor = SvenSettings.GraspAreaDebugColor;
            Color newGraspAreaDebugColor = EditorGUILayout.ColorField("Grasp Area Debug Color", graspAreaDebugColor);

            if (newGraspAreaDebugColor != graspAreaDebugColor)
            {
                EditorPrefs.SetString(SvenSettings._graspAreaDebugColorKey, ColorUtility.ToHtmlStringRGB(newGraspAreaDebugColor));
                refresh = true;
            }

            TripleStoreType tripleStore = SvenSettings.TripleStore;
            TripleStoreType newTripleStore = (TripleStoreType)EditorGUILayout.EnumPopup("Triplestore", tripleStore);
            if (newTripleStore != tripleStore)
            {
                SvenSettings.TripleStore = newTripleStore;
                EditorPrefs.SetInt(SvenSettings._tripleStoreKey, (int)newTripleStore);
                refresh = true;
            }

            string endpointUrl = SvenSettings.EndpointUrl;
            string newEndpointUrl = EditorGUILayout.TextField("Endpoint URL", endpointUrl);
            if (newEndpointUrl != endpointUrl)
            {
                SvenSettings.EndpointUrl = newEndpointUrl;
                EditorPrefs.SetString(SvenSettings._endpointUrlKey, newEndpointUrl);
                refresh = true;
            }
            EditorGUILayout.HelpBox(
                newTripleStore == TripleStoreType.GraphDB
                    ? "GraphDB — Endpoint URL = repository, e.g. http://localhost:7200/repositories/SVEN\nQuery: <endpoint>   |   Graph Store: <endpoint>/rdf-graphs/service"
                    : "Apache Jena (Fuseki) — Endpoint URL = dataset, e.g. http://localhost:3030/SVEN\nQuery: <endpoint>/query   |   Graph Store: <endpoint>/data",
                MessageType.Info);

            string graphName = SvenSettings.GraphName;
            string newGraphName = EditorGUILayout.TextField("Graph Name", graphName);
            if (newGraphName != graphName)
            {
                SvenSettings.GraphName = newGraphName;
                EditorPrefs.SetString(SvenSettings._graphNameKey, newGraphName);
                refresh = true;
            }

            string baseUri = SvenSettings.BaseUri;
            EditorGUILayout.LabelField("Base URI", baseUri);
            if (GUILayout.Button("Copy Base URI"))
            {
                EditorGUIUtility.systemCopyBuffer = baseUri;
            }

            string username = SvenSettings.Username;
            string newUsername = EditorGUILayout.TextField("Username", username);
            if (newUsername != username)
            {
                SvenSettings.Username = newUsername;
                EditorPrefs.SetString(SvenSettings._usernameKey, newUsername);
                refresh = true;
            }

            string password = SvenSettings.Password;
            string newPassword = EditorGUILayout.PasswordField("Password", password);
            if (newPassword != password)
            {
                SvenSettings.Password = newPassword;
                EditorPrefs.SetString(SvenSettings._passwordKey, newPassword);
                refresh = true;
            }

            int semanticizeFrequency = SvenSettings.SemanticizeFrequency;
            int newSemanticizeFrequency = EditorGUILayout.IntSlider("Semanticize Frequency (record/second)", semanticizeFrequency, 1, 60);
            if (newSemanticizeFrequency != semanticizeFrequency)
            {
                SvenSettings.SemanticizeFrequency = newSemanticizeFrequency;
                EditorPrefs.SetInt(SvenSettings._semanticizeFrequencyKey, newSemanticizeFrequency);
                refresh = true;
            }

            int bufferSize = SvenSettings.BufferSize;
            int newBufferSize = EditorGUILayout.IntSlider("Buffer Size (max number of triples stored locally)", bufferSize, 10000, 100000);
            if (newBufferSize != bufferSize)
            {
                SvenSettings.BufferSize = newBufferSize;
                EditorPrefs.SetInt(SvenSettings._bufferSizeKey, newBufferSize);
                refresh = true;
            }

            // ontologies
            EditorGUILayout.LabelField("Ontologies", EditorStyles.boldLabel);
            // create a copy
            var ontologies = new Dictionary<string, string>(SvenSettings.Ontologies);
            foreach (var ontology in ontologies)
            {
                // ontology value is scrollable with a scrollbar, readonly, and has a fixed height of 100 pixels
                EditorGUILayout.BeginHorizontal("GroupBox");
                EditorGUILayout.BeginVertical(GUILayout.Width(200));
                EditorGUILayout.LabelField(ontology.Key, GUILayout.Width(125));
                // remove button for ontology
                if (GUILayout.Button("Remove", GUILayout.ExpandWidth(true)))
                {
                    // delete the files in StreamingAssets/Ontologies/<ontology.Key>.ttl
                    string filePath = System.IO.Path.Combine(Application.streamingAssetsPath, "Ontologies", ontology.Key + ".ttl");
                    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);

                    SvenSettings.Ontologies.Clear();
                    SvenSettings.RefreshConfig();
                }
                EditorGUILayout.EndVertical();
                if (ontologiesIsShown.ContainsKey(ontology.Key) && ontologiesIsShown[ontology.Key])
                {
                    // scrollable labelfield for ontology value
                    if (!ontologiesScrollPosition.ContainsKey(ontology.Key))
                    {
                        ontologiesScrollPosition[ontology.Key] = Vector2.zero;
                    }
                    ontologiesScrollPosition[ontology.Key] = EditorGUILayout.BeginScrollView(ontologiesScrollPosition[ontology.Key], GUILayout.Height(150));
                    // read the content of the file at <ontology.Value>
                    string ontologyContent = System.IO.File.Exists(ontology.Value) ? System.IO.File.ReadAllText(ontology.Value) : "File not found: " + ontology.Value;
                    GUILayout.TextArea(ontologyContent, EditorStyles.textField, GUILayout.ExpandHeight(true));
                    GUILayout.EndScrollView();
                }
                else
                {
                    // button show content of ontology file
                    if (GUILayout.Button("Show Content", GUILayout.ExpandWidth(true), GUILayout.Height(39)))
                    {
                        if (ontologiesIsShown.ContainsKey(ontology.Key))
                            ontologiesIsShown[ontology.Key] = !ontologiesIsShown[ontology.Key];
                        else
                            ontologiesIsShown[ontology.Key] = true;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Refresh Ontologies"))
            {
                SvenSettings.Ontologies.Clear();
                SvenSettings.RefreshConfig();
                RegenerateIndex();
            }
            // import ontology button (select .ttl file, copy it into StreamingAssets/Ontologies, and refresh Ontologies)
            if (GUILayout.Button("Import Ontology"))
            {
                string path = EditorUtility.OpenFilePanel("Select Ontology File", Application.streamingAssetsPath + "/Ontologies", "ttl");
                if (!string.IsNullOrEmpty(path))
                {
                    string fileName = System.IO.Path.GetFileName(path);
                    string destinationPath = System.IO.Path.Combine(Application.streamingAssetsPath, "Ontologies", fileName);
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destinationPath));
                    System.IO.File.Copy(path, destinationPath, true);
                    SvenSettings.Ontologies.Clear();
                    SvenSettings.RefreshConfig();
                    RegenerateIndex();
                }
            }
            if (GUILayout.Button("Open Ontologies Folder"))
            {
                string path = System.IO.Path.Combine(Application.streamingAssetsPath, "Ontologies");
                if (System.IO.Directory.Exists(path))
                {
                    EditorUtility.RevealInFinder(path);
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "The Ontologies folder does not exist. Please create it first.", "OK");
                }
            }

            if (refresh) SvenSettings.RefreshConfig();
        }

        public static void RegenerateIndex()
        {
            string ontologiesPath = Path.Combine(Application.streamingAssetsPath, "Ontologies");
            if (!Directory.Exists(ontologiesPath))
                Directory.CreateDirectory(ontologiesPath);

            var files = Directory.GetFiles(ontologiesPath, "*.ttl")
                .Select(Path.GetFileName)
                .ToArray();

            string json = JsonUtility.ToJson(new OntologyIndex { files = files }, true);
            File.WriteAllText(Path.Combine(ontologiesPath, "ontologies_index.json"), json);

            AssetDatabase.Refresh();
        }

        [System.Serializable]
        public class OntologyIndex
        {
            public string[] files;
        }
    }
}
#endif