// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using NaughtyAttributes;

using Sven.GraphManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sven.Utils
{
    /// <summary>
    /// Supported triplestore backends. Determines how the SPARQL query endpoint and the Graph Store Protocol
    /// URLs are derived from the configured base Endpoint URL.
    /// </summary>
    public enum TripleStoreType
    {
        /// <summary>Ontotext GraphDB. Endpoint URL = repository, e.g. http://localhost:7200/repositories/SVEN</summary>
        GraphDB,
        /// <summary>Apache Jena Fuseki. Endpoint URL = dataset, e.g. http://localhost:3030/SVEN</summary>
        ApacheJena
    }

    /// <summary>
    /// Helper class to manage SVEN settings.
    /// </summary>
    public static class SvenSettings
    {
        #region Enabled
        /// <summary>
        /// Master switch for the whole SVEN library. When false, the environment is NOT semantized: the graph is
        /// not initialized, no SemantizationCore / Interactor / User observes or records anything, and nothing is
        /// sent to the endpoint. Replay (reading an existing graph) is unaffected.
        /// Configurable in the editor (SVEN Settings) or at launch via --sven-enabled=true|false. Default: true.
        /// </summary>
        public static bool Enabled
        {
            get
            {
                if (_enabled.HasValue) return _enabled.Value;
                string argEnabled = Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith("--sven-enabled="))?.Split('=')[1];
                if (!string.IsNullOrEmpty(argEnabled) && bool.TryParse(argEnabled, out bool parsed))
                    _enabled = parsed;
                else
                    _enabled = true;
                return _enabled.Value;
            }
            set
            {
                if (_enabled == value) return;
                _enabled = value;
            }
        }
        private static bool? _enabled = null;
        public static readonly string _enabledKey = "SVEN_Enabled";
        #endregion

        #region UseInside
        public static bool UseInside => _useInside;
        private static bool _useInside = false;
        public static readonly string _useInsideKey = "SVEN_UseInside";
        #endregion

        #region Debug
        public static bool Debug => _debug;
        private static bool _debug = false;
        public static readonly string _debugKey = "SVEN_Debug";
        #endregion

        #region PointOfViewDebugColor
        public static Color PointOfViewDebugColor => _pointOfViewDebugColor;
        private static Color _pointOfViewDebugColor = Color.red;
        public static readonly string _pointOfViewDebugColorKey = "SVEN_PointOfViewDebugColor";
        #endregion

        #region PointerDebugColor
        public static Color PointerDebugColor => _pointerDebugColor;
        private static Color _pointerDebugColor = Color.blue;
        public static readonly string _pointerDebugColorKey = "SVEN_PointerDebugColor";
        #endregion

        #region GraspAreaDebugColor
        public static Color GraspAreaDebugColor => _graspAreaDebugColor;
        private static Color _graspAreaDebugColor = Color.green;
        public static readonly string _graspAreaDebugColorKey = "SVEN_GraspAreaDebugColor";
        #endregion

        #region EndpointUrl
        public static string EndpointUrl
        {
            get
            {
                if (_endpointUrl != null) return _endpointUrl;
                string argsEndpointUrl = Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith("--sven-endpoint-url="))?.Split('=')[1];
                if (!string.IsNullOrEmpty(argsEndpointUrl) && Uri.TryCreate(argsEndpointUrl, UriKind.Absolute, out Uri parsedUri))
                    _endpointUrl = parsedUri.ToString();
                else
                    _endpointUrl = "http://localhost:7200/repositories/SVEN";
                return _endpointUrl;
            }
            set
            {
                if (_endpointUrl == value) return;
                _endpointUrl = value;
            }
        }
        private static string _endpointUrl = null;
        public static readonly string _endpointUrlKey = "SVEN_EndpointUrl";
        #endregion

        #region TripleStore
        /// <summary>
        /// The targeted triplestore backend (GraphDB or Apache Jena/Fuseki). Drives how the SPARQL query endpoint
        /// and the Graph Store Protocol URLs are built from <see cref="EndpointUrl"/>.
        /// </summary>
        public static TripleStoreType TripleStore
        {
            get
            {
                if (_tripleStore.HasValue) return _tripleStore.Value;
                string argTripleStore = Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith("--sven-triplestore="))?.Split('=')[1];
                if (!string.IsNullOrEmpty(argTripleStore) && Enum.TryParse(argTripleStore, true, out TripleStoreType parsed))
                    _tripleStore = parsed;
                else
                    _tripleStore = TripleStoreType.GraphDB;
                return _tripleStore.Value;
            }
            set
            {
                if (_tripleStore == value) return;
                _tripleStore = value;
            }
        }
        private static TripleStoreType? _tripleStore = null;
        public static readonly string _tripleStoreKey = "SVEN_TripleStore";

        /// <summary>
        /// The SPARQL query endpoint derived from <see cref="EndpointUrl"/> and the selected triplestore.
        /// GraphDB: the repository URL itself is the query endpoint.
        /// Apache Jena/Fuseki: the dataset URL + "/query".
        /// </summary>
        public static string SparqlQueryEndpoint
        {
            get
            {
                string baseUrl = EndpointUrl.TrimEnd('/');
                return TripleStore switch
                {
                    TripleStoreType.ApacheJena => baseUrl + "/query",
                    _ => baseUrl,
                };
            }
        }

        /// <summary>
        /// Builds the Graph Store Protocol URL targeting a named graph, according to the selected triplestore.
        /// GraphDB: {base}/rdf-graphs/service?graph=...  Apache Jena/Fuseki: {base}/data?graph=...
        /// </summary>
        /// <param name="graphUri">The absolute URI of the named graph.</param>
        public static string GraphStoreServiceUrl(string graphUri)
        {
            string baseUrl = EndpointUrl.TrimEnd('/');
            string encoded = Uri.EscapeDataString(graphUri);
            return TripleStore switch
            {
                TripleStoreType.ApacheJena => $"{baseUrl}/data?graph={encoded}",
                _ => $"{baseUrl}/rdf-graphs/service?graph={encoded}",
            };
        }
        #endregion

        #region Username
        public static string Username
        {
            get
            {
                if (_username != null) return _username;
                string argsUsername = Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith("--sven-username="))?.Split('=')[1];
                if (!string.IsNullOrEmpty(argsUsername))
                    _username = argsUsername;
                else
                    _username = "admin";
                return _username;
            }
            set
            {
                if (_username == value) return;
                _username = value;
            }
        }
        private static string _username = null;
        public static readonly string _usernameKey = "SVEN_Username";
        #endregion

        #region Password
        public static string Password
        {
            get
            {
                if (_password != null) return _password;
                string argsPassword = Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith("--sven-password="))?.Split('=')[1];
                if (!string.IsNullOrEmpty(argsPassword))
                    _password = argsPassword;
                else
                    _password = "admin";
                return _password;
            }
            set
            {
                if (_password == value) return;
                _password = value;
            }
        }
        private static string _password = null;
        public static readonly string _passwordKey = "SVEN_Password";
        #endregion

        #region SemanticizeFrequency
        public static int SemanticizeFrequency
        {
            get
            {
                if (0 < _semanticizeFrequency && _semanticizeFrequency <= 60) return _semanticizeFrequency;
                string argsSemanticizeFrequency = Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith("--semanticize-frequency="))?.Split('=')[1];
                if (int.TryParse(argsSemanticizeFrequency, out int parsedFrequency) && parsedFrequency > 0 && parsedFrequency <= 60)
                    _semanticizeFrequency = parsedFrequency;
                else
                    _semanticizeFrequency = 10;
                return _semanticizeFrequency;
            }
            set
            {
                if (_semanticizeFrequency == value) return;
                if (value < 1 || value > 60) throw new ArgumentOutOfRangeException(nameof(value), "Semanticize frequency must be between 1 and 60 seconds.");
                _semanticizeFrequency = value;
            }
        }
        private static int _semanticizeFrequency = 0;
        public static readonly string _semanticizeFrequencyKey = "SVEN_SemanticizeFrequency";
        #endregion

        #region BufferSize
        public static int BufferSize
        {
            get
            {
                if (10000 <= _bufferSize && _bufferSize <= 100000) return _bufferSize;
                string argsBufferSize = Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith("--buffer-size="))?.Split('=')[1];
                if (int.TryParse(argsBufferSize, out int parsedSize) && parsedSize >= 10000 && parsedSize <= 100000)
                    _bufferSize = parsedSize;
                else
                    _bufferSize = 20000;
                return _bufferSize;
            }
            set
            {
                if (_bufferSize == value) return;
                if (value < 10000 || value > 100000) throw new ArgumentOutOfRangeException(nameof(value), "Buffer size must be between 10,000 and 100,000 triples.");
                _bufferSize = value;
            }
        }
        private static int _bufferSize = 0;
        public static readonly string _bufferSizeKey = "SVEN_BufferSize";
        #endregion

        #region Ontologies
        public static Dictionary<string, string> Ontologies
        {
            get
            {

                if (_ontologies.Count == 0)
                {
                    string ontologiesPath = StreamingAssetsPath + "/Ontologies";
                    if (System.IO.Directory.Exists(ontologiesPath))
                    {
                        string[] ontologyFiles = System.IO.Directory.GetFiles(ontologiesPath, "*.ttl");
                        foreach (string file in ontologyFiles)
                        {
                            string ontologyName = System.IO.Path.GetFileNameWithoutExtension(file);
                            string absolutePath = System.IO.Path.GetFullPath(file);
                            _ontologies[ontologyName] = absolutePath;
                        }
                    }
                    else
                    {
                        System.IO.Directory.CreateDirectory(ontologiesPath);

                        string svenTtlPath = System.IO.Path.Combine(ontologiesPath, "sven.ttl");
                        try
                        {
                            using (var client = new System.Net.WebClient())
                            {
                                client.DownloadFile("https://sven.lisn.upsaclay.fr/ontology#", svenTtlPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogError("Failed to download SVEN ontology: " + ex.Message);
                            System.IO.File.WriteAllText(svenTtlPath, "# SVEN ontology\n# Voir : https://sven.lisn.upsaclay.fr/ontology#\n");
                        }

                        _ontologies["sven"] = System.IO.Path.GetFullPath(svenTtlPath);
                    }
                }
                return _ontologies;
            }
        }
        private static Dictionary<string, string> _ontologies = new();
        public static readonly string _ontologiesKey = "SVEN_Ontologies";
        #endregion


        #region MainThreadPaths
        private static string _streamingAssetsPath;
        private static string _persistentDataPath;

        /// <summary>
        /// Cached <see cref="Application.streamingAssetsPath"/>. Application.* members can only be read on the main
        /// thread, so the value is captured once (see <see cref="CacheMainThreadPaths"/>) and reused everywhere,
        /// including from background threads (Task.Run).
        /// </summary>
        public static string StreamingAssetsPath
        {
            get
            {
                if (string.IsNullOrEmpty(_streamingAssetsPath))
                    _streamingAssetsPath = Application.streamingAssetsPath;
                return _streamingAssetsPath;
            }
        }

        /// <summary>
        /// Cached <see cref="Application.persistentDataPath"/>, captured on the main thread for use from background threads.
        /// </summary>
        public static string PersistentDataPath
        {
            get
            {
                if (string.IsNullOrEmpty(_persistentDataPath))
                    _persistentDataPath = Application.persistentDataPath;
                return _persistentDataPath;
            }
        }

        /// <summary>
        /// Caches the Unity Application paths on the main thread. Must be called once from the main thread
        /// (e.g. GraphController.Awake) before any code path that may read them from a background thread.
        /// </summary>
        public static void CacheMainThreadPaths()
        {
            _streamingAssetsPath = Application.streamingAssetsPath;
            _persistentDataPath = Application.persistentDataPath;
        }
        #endregion


        #region BaseUri


        [ShowNativeProperty] public static string BaseUri => "https://sven.lisn.upsaclay.fr/ve/" + _graphName + "/";
        [SerializeField] private static string _graphName = "Default";
        public static readonly string _graphNameKey = "SVEN_GraphName";
        public static string GraphName
        {
            get => _graphName;
            set
            {
                if (string.IsNullOrEmpty(value)) return;
                if (_graphName == value) return;
                _graphName = value;
                GraphManager.SetBaseUri(BaseUri);
                GraphManager.SetNamespace("", BaseUri);
            }
        }

        #endregion

#if UNITY_EDITOR
        public static void RefreshConfig()
        {
            try
            {
                _enabled = EditorPrefs.GetBool(_enabledKey, Enabled);
                _useInside = EditorPrefs.GetBool(_useInsideKey, UseInside);
                _debug = EditorPrefs.GetBool(_debugKey, Debug);
                _pointOfViewDebugColor = ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString(_pointOfViewDebugColorKey, null), out Color pointOfViewDebugColor) ? pointOfViewDebugColor : PointOfViewDebugColor;
                _pointerDebugColor = ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString(_pointerDebugColorKey, null), out Color pointerDebugColor) ? pointerDebugColor : PointerDebugColor;
                _graspAreaDebugColor = ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString(_graspAreaDebugColorKey, null), out Color graspAreaDebugColor) ? graspAreaDebugColor : GraspAreaDebugColor;
                _endpointUrl = EditorPrefs.GetString(_endpointUrlKey, EndpointUrl);
                _tripleStore = (TripleStoreType)EditorPrefs.GetInt(_tripleStoreKey, (int)TripleStore);
                _username = EditorPrefs.GetString(_usernameKey, Username);
                _password = EditorPrefs.GetString(_passwordKey, Password);
                _semanticizeFrequency = EditorPrefs.GetInt(_semanticizeFrequencyKey, SemanticizeFrequency);
                _bufferSize = EditorPrefs.GetInt(_bufferSizeKey, BufferSize);
                _ontologies = Ontologies;
                _graphName = EditorPrefs.GetString(_graphNameKey, GraphName);
            }
            catch { }
        }

        static SvenSettings()
        {
            RefreshConfig();
        }
#endif

        public static async Task<Dictionary<string, string>> GetOntologiesAsync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string indexPath = StreamingAssetsPath + "/Ontologies/ontologies_index.json";
            using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(indexPath))
            {
                await request.SendWebRequest();
                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    UnityEngine.Debug.LogError("Failed to load ontologies index: " + request.error);
                    return new Dictionary<string, string>();
                }
                var index = JsonUtility.FromJson<OntologyIndex>(request.downloadHandler.text);
                var dict = new Dictionary<string, string>();
                foreach (var file in index.files)
                {
                    string name = System.IO.Path.GetFileNameWithoutExtension(file);
                    string url = StreamingAssetsPath + "/Ontologies/" + file;
                    dict[name] = url;
                }
                return dict;
            }
#else
            // Application.streamingAssetsPath can only be read on the main thread: read it here (caller thread,
            // main during init) BEFORE entering Task.Run, otherwise a build throws
            // "UnityException: get_streamingAssetsPath can only be called from the main thread".
            string ontologiesPath = StreamingAssetsPath + "/Ontologies";
            return await Task.Run(() =>
            {
                if (_ontologies.Count == 0)
                {
                    if (System.IO.Directory.Exists(ontologiesPath))
                    {
                        string[] ontologyFiles = System.IO.Directory.GetFiles(ontologiesPath, "*.ttl");
                        foreach (string file in ontologyFiles)
                        {
                            string ontologyName = System.IO.Path.GetFileNameWithoutExtension(file);
                            string absolutePath = System.IO.Path.GetFullPath(file);
                            _ontologies[ontologyName] = absolutePath;
                        }
                    }
                }
                return _ontologies;
            });
#endif
        }

        [System.Serializable]
        private class OntologyIndex
        {
            public string[] files;
        }
    }
}