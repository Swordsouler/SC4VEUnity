// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using PrimeTween;
using Sven.Content;
using Sven.GraphManagement.Description;
using Sven.OwlTime;
using Sven.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Nodes;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Inference;
using VDS.RDF.Update;
using VDS.RDF.Writing;
#if UNITY_WEBGL && !UNITY_EDITOR
using UnityEngine.Networking;
#endif

namespace Sven.GraphManagement
{
    public static class GraphManager
    {
        private static readonly Graph _instance = new();
        public static Graph Instance => _instance;
        private static readonly object _graphLock = new();
        private static bool _isFlushing = false;
        // Backoff state: when the endpoint is unreachable, a flush must NOT be retried on every Assert
        // (each retry re-copies and re-serializes the whole graph). We wait an increasing delay between attempts.
        private static DateTime _nextFlushRetryUtc = DateTime.MinValue;
        private static int _consecutiveFlushFailures = 0;
        // Hard memory ceiling (as a multiple of BufferSize). Past it, a failed batch is spooled to a local
        // backup file and freed from memory so an unreachable endpoint cannot grow RAM without bound.
        private const int MaxBufferMultiplier = 3;
        private static int _backupCounter = 0;
        public static int Count
        {
            get
            {
                lock (_graphLock)
                {
                    return _instance.Triples.Count;
                }
            }
        }
        private static readonly Dictionary<string, string> _ontologies = new();
        private static readonly List<Instant> _instants = new();
        private static AuthenticationHeaderValue _authenticationHeaderValue = null;
        public static DateTime StartedAt => _instants.Count > 0 ? _instants[0].inXSDDateTime : DateTime.Now;
        public static DateTime EndedAt => _instants.Count > 0 ? _instants[^1].inXSDDateTime : DateTime.Now;
        public static float Duration => (float)(EndedAt - StartedAt).TotalSeconds;
        public static Instant CurrentInstantLoaded { get; private set; } = null;
        public static string BaseUri
        {
            get
            {
                lock (_graphLock)
                {
                    return _instance.BaseUri?.AbsoluteUri ?? string.Empty;
                }
            }
        }
        public static string GraphName => BaseUri.Split("/")[^2];
        private static bool _isGraphInitialized = false;
        public static bool IsGraphInitialized
        {
            get => _isGraphInitialized;
            set => _isGraphInitialized = value;
        }

        /// <summary>
        /// The current Basic authentication header value (e.g. "Basic dXNlcjpwYXNz"), or null if not set.
        /// Exposed so the WebGL query path (UnityWebRequest) can attach the same auth as the desktop HttpClient.
        /// </summary>
        public static string AuthorizationHeader => _authenticationHeaderValue?.ToString();

        public static void SetAuthenticationHeaderValue(string username, string password)
        {
            if (string.IsNullOrEmpty(username)) throw new ArgumentNullException(nameof(username) + " is null or empty.");
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password) + " is null or empty.");
            _authenticationHeaderValue = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
        }

        public static bool HasNamespace(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) throw new ArgumentNullException(nameof(prefix) + " is null or empty.");
            lock (_graphLock)
            {
                return _instance.NamespaceMap.HasNamespace(prefix);
            }
        }
        public static void Clear()
        {
            lock (_graphLock)
            {
                _instance.Clear();
            }
            _ontologies.Clear();
            _instants.Clear();
        }

        /// <summary>
        /// Decode the graph to a turtle string.
        /// </summary>
        /// <param name="graph">The graph to decode.</param>
        /// <returns>Decoded graph in turtle format.</returns>
        public static string DecodeGraph(IGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph) + " is null.");
            using System.IO.StringWriter sw = new();
            SaveGraph(graph, sw);
            return sw.ToString();
        }

        /// <summary>
        /// Saves a graph to the given TextWriter.
        /// </summary>
        /// <param name="graph">The graph to save.</param>
        /// <param name="writer">The TextWriter to save to.</param>
        public static void SaveGraph(IGraph graph, TextWriter writer)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            CompressingTurtleWriter turtleWriter = new(TurtleSyntax.Rdf11Star);
            turtleWriter.Save(graph, writer);
        }
        public static string DecodeGraph()
        {
            Graph g = new();
            lock (_graphLock)
            {
                g.NamespaceMap.Import(_instance.NamespaceMap);
                g.BaseUri = _instance.BaseUri;
                g.Assert(_instance.Triples);
            }
            return DecodeGraph(g);
        }

        public static async Task Reload()
        {
            PrimeTweenConfig.warnEndValueEqualsCurrent = false;
            Clear();
            await LoadOntologiesAsync();
            SetBaseUri(SvenSettings.BaseUri);
            SetNamespace("", SvenSettings.BaseUri);
            SetAuthenticationHeaderValue(SvenSettings.Username, SvenSettings.Password);
            IsGraphInitialized = true;
        }

        public static void SetBaseUri(string baseUri)
        {
            if (string.IsNullOrEmpty(baseUri)) throw new ArgumentNullException(nameof(baseUri) + " is null or empty.");
            if (_instance == null) throw new InvalidOperationException("Graph instance is not initialized.");
            lock (_graphLock)
            {
                _instance.BaseUri = new Uri(baseUri);
            }
        }

        public static void SetNamespace(string prefix, string uri)
        {
            if (string.IsNullOrEmpty(uri)) throw new ArgumentNullException(nameof(uri) + " is null or empty.");
            if (_instance == null) throw new InvalidOperationException("Graph instance is not initialized.");
            lock (_graphLock)
            {
                _instance.NamespaceMap.AddNamespace(prefix, UriFactory.Create(uri));
            }
        }

        public static async Task ApplyOntologyAsync(Graph graph)
        {
            Graph ontologyGraph = new();
            Dictionary<string, string> ontologies = await SvenSettings.GetOntologiesAsync();
            TurtleParser turtleParser = new();
            foreach (KeyValuePair<string, string> ontology in ontologies)
            {
                turtleParser.Load(ontologyGraph, ontology.Value);
            }
            StaticRdfsReasoner reasoner = new();
            reasoner.Initialise(ontologyGraph);
            reasoner.Apply(graph);
        }

        public static async Task LoadOntologyAsync(string ontologyName, string ontologyFileName)
        {
            if (string.IsNullOrEmpty(ontologyName)) throw new ArgumentNullException(nameof(ontologyName) + " is null or empty.");
            if (string.IsNullOrEmpty(ontologyFileName)) throw new ArgumentNullException(nameof(ontologyFileName) + " is null or empty.");

#if UNITY_WEBGL && !UNITY_EDITOR
            using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(ontologyFileName))
            {
                await request.SendWebRequest();
                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                    throw new Exception("Failed to load ontology: " + request.error);

                string ttlContent = request.downloadHandler.text;
                await Task.Run(() => {
                    TurtleParser turtleParser = new();
                    using (var reader = new System.IO.StringReader(ttlContent))
                    {
                        lock (_graphLock)
                        {
                            turtleParser.Load(_instance, reader);
                        }
                    }
                });
            }
#else
            await Task.Run(() =>
            {
                TurtleParser turtleParser = new();
                lock (_graphLock)
                {
                    turtleParser.Load(_instance, ontologyFileName);
                }
            });
#endif
            if (!_ontologies.ContainsKey(ontologyName))
                _ontologies.Add(ontologyName, ontologyFileName);
        }

        public static async Task LoadOntologiesAsync()
        {
            MapppedComponents.LoadAllMappedComponents();
            Dictionary<string, string> ontologies = await SvenSettings.GetOntologiesAsync();
            foreach (KeyValuePair<string, string> ontology in ontologies)
                await LoadOntologyAsync(ontology.Key, ontology.Value);
        }

        public static async Task<Graph> GetInferredGraphAsync(List<Triple> triples, Uri baseUri, NamespaceMapper nsMap)
        {
            Graph workGraph = new()
            {
                // Utiliser les métadonnées fournies, sans aucun accès au graphe global.
                BaseUri = baseUri
            };
            workGraph.NamespaceMap.Import(nsMap);
            workGraph.Assert(triples);

            Graph ontologyGraph = new();
            // Le chargement des ontologies est déjà thread-safe.
            foreach (var ontology in _ontologies)
            {
                try
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(ontology.Value))
                    {
                        await request.SendWebRequest();
                        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                            throw new Exception("Failed to load ontology: " + request.error);

                        string ttlContent = request.downloadHandler.text;
                        await Task.Run(() => {
                            TurtleParser turtleParser = new();
                            using (var reader = new System.IO.StringReader(ttlContent))
                            {
                                turtleParser.Load(ontologyGraph, reader);
                            }
                        });
                    }
#else
                    await Task.Run(() =>
                    {
                        TurtleParser turtleParser = new();
                        turtleParser.Load(ontologyGraph, ontology.Value);
                    });
#endif
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to load ontology \"{ontology.Key}\": {ex.Message}", ex);
                }
            }

            try
            {
                // L'inférence est l'opération la plus lourde. Elle se fait ici en totale isolation.
                await Task.Run(() =>
                {
                    StaticRdfsReasoner reasoner = new();
                    reasoner.Initialise(ontologyGraph);
                    reasoner.Apply(workGraph);
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to apply reasoning rules: {ex.Message}", ex);
            }
            return workGraph;
        }

        public static async Task SaveToFile(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) throw new ArgumentNullException(nameof(absolutePath) + " is null or empty.");
            if (!System.IO.Path.IsPathRooted(absolutePath)) throw new ArgumentException("The path must be absolute.", nameof(absolutePath));
            try
            {
                string turtleContent = await Task.Run(() => DecodeGraph());
                await File.WriteAllTextAsync(absolutePath, turtleContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save graph to file: {ex.Message}", ex);
            }
        }

        public static async Task AddToEndpoint()
        {
            List<Triple> triplesToFlush;
            Uri baseUriToFlush;
            NamespaceMapper nsMapToFlush;
            lock (_graphLock)
            {
                triplesToFlush = new List<Triple>(_instance.Triples);
                baseUriToFlush = _instance.BaseUri;
                nsMapToFlush = new NamespaceMapper();
                nsMapToFlush.Import(_instance.NamespaceMap);
            }
            await AddToEndpoint(triplesToFlush, baseUriToFlush, nsMapToFlush);
        }

        public static async Task AddToEndpoint(List<Triple> triplesToFlush, Uri baseUri, NamespaceMapper nsMap)
        {
            await Task.Run(async () =>
            {
                string endpointUrl = SvenSettings.EndpointUrl;
                Debug.Log($"Saving graph to endpoint: {endpointUrl}");

                if (string.IsNullOrEmpty(endpointUrl)) throw new ArgumentNullException(nameof(endpointUrl) + " is null or empty.");
                if (!Uri.IsWellFormedUriString(endpointUrl, UriKind.Absolute)) throw new ArgumentException("The endpoint URL is not valid.", nameof(endpointUrl));

                MimeTypeDefinition writerMimeTypeDefinition = MimeTypesHelper.GetDefinitions("application/x-turtle").First();

                Graph graphToSend = new();
                graphToSend.Assert(triplesToFlush);
                graphToSend.BaseUri = baseUri;
                graphToSend.NamespaceMap.Import(nsMap);

                string tempFilePath = Path.GetTempFileName();
                try
                {
                    // Étape 1: Écrire directement dans un fichier temporaire pour éviter une charge mémoire élevée.
                    using (StreamWriter sw = new(tempFilePath, false, Encoding.UTF8))
                    {
                        SaveGraph(graphToSend, sw);
                    }

                    string serviceUrl = SvenSettings.GraphStoreServiceUrl(baseUri.AbsoluteUri);
                    using HttpClient httpClient = new();
                    httpClient.DefaultRequestHeaders.Authorization = _authenticationHeaderValue;

                    // Étape 2: Utiliser StreamContent pour lire le fichier et l'envoyer sans le charger entièrement en mémoire.
                    using FileStream fs = new(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                    using StreamContent streamContent = new(fs);
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(writerMimeTypeDefinition.CanonicalMimeType) { CharSet = "utf-8" };

                    HttpRequestMessage request = new(HttpMethod.Post, serviceUrl)
                    {
                        Content = streamContent
                    };

                    using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(request).ConfigureAwait(false);

                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        Debug.Log("Graph added to endpoint.");
                    }
                    else
                    {
                        throw new Exception("Failed to add the graph to the endpoint. " + httpResponseMessage.ReasonPhrase);
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to add graph to endpoint: {ex}", ex);
                }
                finally
                {
                    // Étape 3: Nettoyer le fichier temporaire.
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }
            }).ConfigureAwait(false);
        }

        public static async Task SaveToEndpoint()
        {
            string endpointUrl = SvenSettings.EndpointUrl;
            Debug.Log($"Saving graph to endpoint: {endpointUrl}");
            if (string.IsNullOrEmpty(endpointUrl)) throw new ArgumentNullException(nameof(endpointUrl) + " is null or empty.");
            if (!Uri.IsWellFormedUriString(endpointUrl, UriKind.Absolute)) throw new ArgumentException("The endpoint URL is not valid.", nameof(endpointUrl));

            MimeTypeDefinition writerMimeTypeDefinition = MimeTypesHelper.GetDefinitions("application/x-turtle").First();
            string turtleContent = await Task.Run(async () =>
            {
                List<Triple> triples;
                Uri baseUri;
                NamespaceMapper nsMap;
                lock (_graphLock)
                {
                    triples = _instance.Triples.ToList();
                    baseUri = _instance.BaseUri;
                    nsMap = new NamespaceMapper();
                    nsMap.Import(_instance.NamespaceMap);
                }
                return DecodeGraph(await GetInferredGraphAsync(triples, baseUri, nsMap));
            });
            string serviceUrl = SvenSettings.GraphStoreServiceUrl(BaseUri);
            try
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                using HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.Authorization = _authenticationHeaderValue;

                HttpRequestMessage request = new(HttpMethod.Put, serviceUrl)
                {
                    Content = new StringContent(turtleContent, Encoding.UTF8, writerMimeTypeDefinition.CanonicalMimeType)
                };

                using HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(request);

                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    Debug.Log("Graph saved to endpoint.");
                    return;
                }

                throw new Exception("Failed to save the graph to the endpoint. " + httpResponseMessage.ReasonPhrase);
#else
                using UnityWebRequest request = new(serviceUrl, UnityWebRequest.kHttpVerbPUT)
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(turtleContent))
                    {
                        contentType = writerMimeTypeDefinition.CanonicalMimeType
                    },
                    downloadHandler = new DownloadHandlerBuffer()
                };
                request.SetRequestHeader("Access-Control-Allow-Origin", "*");
                request.SetRequestHeader("Accept", writerMimeTypeDefinition.CanonicalMimeType);
                request.SetRequestHeader("Authorization", _authenticationHeaderValue.ToString());

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Graph saved to endpoint.");
                    return;
                }

                throw new Exception("Failed to save the graph to the endpoint. " + request.error);
#endif
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save graph to endpoint: {ex}", ex);
            }
        }

        public static async Task LoadFromEndpointAsync(string endpointUrl)
        {
            if (string.IsNullOrEmpty(endpointUrl)) throw new ArgumentNullException(nameof(endpointUrl) + " is null or empty.");
            if (!Uri.IsWellFormedUriString(endpointUrl, UriKind.Absolute)) throw new ArgumentException("The endpoint URL is not valid.", nameof(endpointUrl));

            string query = @"
SELECT ?s ?p ?o
WHERE {
    ?s ?p ?o .
} LIMIT 1000000";

            SparqlResultSet results = await QueryEndpoint(endpointUrl, query);

            if (results == null || results.Count == 0)
            {
                Debug.LogWarning("No results found in the graph at the endpoint.");
                return;
            }
            await Task.Run(() =>
            {
                lock (_graphLock)
                {
                    foreach (var result in results)
                    {
                        INode subject = result["s"];
                        INode predicate = result["p"];
                        INode @object = result["o"];
                        if (subject != null && predicate != null && @object != null)
                        {
                            _instance.Assert(new Triple(subject, predicate, @object));
                        }
                    }
                }
            });
        }

        public static async Task LoadFromFileAsync(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) throw new ArgumentNullException(nameof(absolutePath) + " is null or empty.");
            if (!System.IO.Path.IsPathRooted(absolutePath)) throw new ArgumentException("The path must be absolute.", nameof(absolutePath));
            if (!File.Exists(absolutePath)) throw new FileNotFoundException($"File not found: {absolutePath}");
            await Task.Run(() =>
            {
                lock (_graphLock)
                {
                    _instance.LoadFromFile(absolutePath);
                }
            });
        }


        #region Time Management

        /// <summary>
        /// Current instant.
        /// </summary>
        private static Instant _currentInstant;
        public static Instant CurrentInstant
        {
            get
            {
                // example : instantPerSecond = 10 -> if now = 2021-10-10T10:10:10.0516051 then dateTime will be 2021-10-10T10:10:10.0000000
                DateTime dateTime = FormatDateTime(DateTime.Now);
                if (_currentInstant == null || _currentInstant.inXSDDateTime != dateTime)
                    CurrentInstant = new Instant(dateTime);
                return _currentInstant;
            }
            private set
            {
                if (_currentInstant == value) return;
                _currentInstant = value;
                _currentInstant.Semanticize();
                _instants.Add(value);
            }
        }

        /// <summary>
        /// Format the DateTime to the instantPerSecond.
        /// </summary>
        /// <param name="dateTime">The DateTime to format.</param>
        /// <returns>DateTime.</returns>
        private static DateTime FormatDateTime(DateTime dateTime)
        {
            return new(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond / (1000 / SvenSettings.SemanticizeFrequency) * (1000 / SvenSettings.SemanticizeFrequency));
        }

        #endregion


        public static void Assert(IEnumerable<Triple> triples)
        {
            foreach (Triple t in triples)
            {
                try
                {
                    Assert(t);
                }
                catch (Exception e)
                {
                    if (SvenSettings.Debug)
                        Debug.LogError($"Failed to assert triple {t}: {e}");
                }
            }
        }

        public static IUriNode Assert(Triple t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));

            IUriNode subject = t.Subject as IUriNode ?? throw new ArgumentException("The subject of the triple must be an IUriNode.", nameof(t));
            List<Triple> triplesToFlush = null;
            Uri baseUriToFlush = null;
            NamespaceMapper nsMapToFlush = null;

            lock (_graphLock)
            {
                _instance.Assert(t);
                if (_instance.Triples.Count >= SvenSettings.BufferSize && !_isFlushing && DateTime.UtcNow >= _nextFlushRetryUtc)
                {
                    _isFlushing = true;
                    // Copier TOUTES les données nécessaires à l'intérieur d'un seul verrou court.
                    triplesToFlush = new List<Triple>(_instance.Triples);
                    baseUriToFlush = _instance.BaseUri;
                    nsMapToFlush = new NamespaceMapper();
                    nsMapToFlush.Import(_instance.NamespaceMap);
                }
            }

            if (triplesToFlush != null)
            {
                // Lancer la tâche de fond avec sa propre copie de TOUTES les données.
                FlushBufferToEndpointAsync(triplesToFlush, baseUriToFlush, nsMapToFlush).FireAndForget();
            }
            return subject;
        }

        public static async Task ForceFlushToEndpointAsync()
        {
            List<Triple> triplesToFlush;
            Uri baseUriToFlush;
            NamespaceMapper nsMapToFlush;
            lock (_graphLock)
            {
                triplesToFlush = new List<Triple>(_instance.Triples);
                baseUriToFlush = _instance.BaseUri;
                nsMapToFlush = new NamespaceMapper();
                nsMapToFlush.Import(_instance.NamespaceMap);
            }
            await FlushBufferToEndpointAsync(triplesToFlush, baseUriToFlush, nsMapToFlush);
        }

        public static async Task FlushBufferToEndpointAsync(List<Triple> triplesToFlush, Uri baseUri, NamespaceMapper nsMap)
        {
            try
            {
                string endpointUrl = SvenSettings.EndpointUrl;
                if (string.IsNullOrEmpty(endpointUrl) || !Uri.IsWellFormedUriString(endpointUrl, UriKind.Absolute))
                {
                    Debug.LogError("L'URL du point de terminaison n'est pas valide. Impossible de vider le tampon.");
                    return;
                }

                // Préparation et envoi du graphe à partir des données fournies
                await AddToEndpoint(triplesToFlush, baseUri, nsMap);
                // delete triplesttoFlush from memory
                lock (_graphLock)
                {
                    foreach (Triple t in triplesToFlush)
                    {
                        _instance.Retract(t);
                    }
                }
                // Le flush a réussi : on remet le compteur d'échecs et la fenêtre de backoff à zéro.
                _consecutiveFlushFailures = 0;
                _nextFlushRetryUtc = DateTime.MinValue;
                await SyncWithEndpoint();
                Debug.Log("Buffer memory cleared. Now contains " + Count + " triples.");
            }
            catch (Exception ex)
            {
                // Échec (endpoint injoignable, 4xx/5xx, timeout...). On NE retire PAS les triples : ils restent
                // en mémoire pour être renvoyés plus tard. On programme un backoff exponentiel (plafonné à 30 s)
                // pour ne pas re-sérialiser tout le graphe à chaque Assert ni marteler le réseau.
                _consecutiveFlushFailures++;
                double backoffSeconds = Math.Min(30.0, Math.Pow(2, Math.Min(_consecutiveFlushFailures, 5)));
                _nextFlushRetryUtc = DateTime.UtcNow.AddSeconds(backoffSeconds);
                Debug.LogError($"Échec du vidage du tampon (tentative {_consecutiveFlushFailures}, prochaine dans {backoffSeconds:0}s) : {ex.Message}");

                // Garde-fou mémoire : si le tampon a dépassé le plafond (endpoint durablement injoignable),
                // on écrit le lot dans une sauvegarde locale et on le libère, pour éviter une fuite mémoire non bornée.
                int count;
                lock (_graphLock) { count = _instance.Triples.Count; }
                if (count >= SvenSettings.BufferSize * MaxBufferMultiplier)
                    TrySpoolToBackup(triplesToFlush, baseUri, nsMap);
            }
            finally
            {
                _isFlushing = false;
            }
        }

        /// <summary>
        /// Flushes the in-memory buffer to the endpoint synchronously, blocking the caller until done (or timed out).
        /// Meant for application shutdown (OnApplicationQuit) where coroutines/async continuations can no longer run.
        /// Safe to block the main thread: AddToEndpoint is ConfigureAwait(false) throughout, so no continuation needs
        /// the main thread. If the endpoint is unreachable/slow, the batch is written to a local backup instead of lost.
        /// </summary>
        public static void ForceFlushToEndpointBlocking(int timeoutSeconds = 10)
        {
            List<Triple> triplesToFlush;
            Uri baseUri;
            NamespaceMapper nsMap;
            lock (_graphLock)
            {
                if (_instance.Triples.Count == 0) return;
                triplesToFlush = new List<Triple>(_instance.Triples);
                baseUri = _instance.BaseUri;
                nsMap = new NamespaceMapper();
                nsMap.Import(_instance.NamespaceMap);
            }

            bool sent = false;
            try
            {
                Task flushTask = Task.Run(() => AddToEndpoint(triplesToFlush, baseUri, nsMap));
                sent = flushTask.Wait(TimeSpan.FromSeconds(timeoutSeconds)) && !flushTask.IsFaulted;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Final flush on quit failed: {ex.Message}");
            }

            if (sent)
            {
                lock (_graphLock)
                {
                    foreach (Triple t in triplesToFlush)
                        _instance.Retract(t);
                }
                Debug.Log($"Final flush on quit: {triplesToFlush.Count} triples sent to endpoint.");
            }
            else
            {
                // Endpoint injoignable/lent au moment de quitter : on persiste localement pour ne pas perdre la session.
                TrySpoolToBackup(triplesToFlush, baseUri, nsMap);
            }
        }

        /// <summary>
        /// Writes a batch of triples to a local backup Turtle file (PersistentDataPath/SVEN_Backup) and frees them
        /// from memory. Last-resort persistence so an unreachable endpoint never causes data loss or unbounded RAM.
        /// </summary>
        private static void TrySpoolToBackup(List<Triple> triples, Uri baseUri, NamespaceMapper nsMap)
        {
            try
            {
                if (triples == null || triples.Count == 0) return;
                string dir = Path.Combine(SvenSettings.PersistentDataPath, "SVEN_Backup");
                Directory.CreateDirectory(dir);

                Graph g = new();
                g.Assert(triples);
                g.BaseUri = baseUri;
                if (nsMap != null) g.NamespaceMap.Import(nsMap);

                string file = Path.Combine(dir, $"sven_backup_{_backupCounter++}.ttl");
                using (StreamWriter sw = new(file, false, Encoding.UTF8))
                {
                    SaveGraph(g, sw);
                }

                // Libère le lot sauvegardé pour borner la mémoire.
                lock (_graphLock)
                {
                    foreach (Triple t in triples)
                        _instance.Retract(t);
                }
                Debug.LogWarning($"SVEN : endpoint injoignable — {triples.Count} triplets écrits dans la sauvegarde locale '{file}' et libérés de la mémoire. À ré-importer quand l'endpoint sera de nouveau disponible.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"SVEN : échec de l'écriture de la sauvegarde locale (les données restent en mémoire) : {ex}");
            }
        }

        private static async Task SyncWithEndpoint()
        {
            string endpointUrl = SvenSettings.SparqlQueryEndpoint;
            string query = $@"PREFIX : <{BaseUri}>
PREFIX time: <http://www.w3.org/2006/time#>
PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>

SELECT DISTINCT ?s ?p ?o ?interval ?intervalP ?intervalO ?instant ?instantP ?instantO
FROM :
WHERE {{
    ?s ?p ?o .
    ?s sven:hasTemporalExtent ?interval .
    ?interval ?intervalP ?intervalO ;
    		  time:hasBeginning ?instant .
    ?instant ?instantP ?instantO .
    FILTER NOT EXISTS {{
    	?interval time:hasEnd ?hasEnd .
    }}
}} LIMIT {SvenSettings.BufferSize}";
            SparqlResultSet results = await QueryEndpoint(endpointUrl, query);
            lock (_graphLock)
            {
                foreach (var result in results)
                {
                    INode subject = result["s"];
                    INode predicate = result["p"];
                    INode @object = result["o"];
                    if (subject != null && predicate != null && @object != null)
                    {
                        _instance.Assert(new Triple(subject, predicate, @object));
                    }
                    INode interval = result["interval"];
                    INode intervalP = result["intervalP"];
                    INode intervalO = result["intervalO"];
                    if (interval != null && intervalP != null && intervalO != null)
                    {
                        _instance.Assert(new Triple(interval, intervalP, intervalO));
                    }
                    INode instant = result["instant"];
                    INode instantP = result["instantP"];
                    INode instantO = result["instantO"];
                    if (instant != null && instantP != null && instantO != null)
                    {
                        _instance.Assert(new Triple(instant, instantP, instantO));
                    }
                }
            }
            await LoadOntologiesAsync();
            SetBaseUri(SvenSettings.BaseUri);
            SetNamespace("", SvenSettings.BaseUri);
        }

        public static IUriNode CreateUriNode(string uri)
        {
            if (string.IsNullOrEmpty(uri)) throw new ArgumentNullException(nameof(uri) + " is null or empty.");
            lock (_graphLock)
            {
                return _instance.CreateUriNode(uri);
            }
        }

        public static ILiteralNode CreateLiteralNode(string name)
        {
            if (string.IsNullOrEmpty(name)) name = string.Empty;
            lock (_graphLock)
            {
                return _instance.CreateLiteralNode(name);
            }
        }

        public static ILiteralNode CreateLiteralNode(string name, Uri uri)
        {
            if (string.IsNullOrEmpty(name)) name = string.Empty;
            if (uri == null) throw new ArgumentNullException(nameof(uri) + " is null.");
            lock (_graphLock)
            {
                return _instance.CreateLiteralNode(name, uri);
            }
        }

        public static INode CreateTripleNode(Triple triple)
        {
            lock (_graphLock)
            {
                return _instance.CreateTripleNode(triple);
            }
        }

        public static async Task<SparqlResultSet> QueryEndpoint(string endpointUrl, string query)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return await Task.Run(async () =>
            {
#endif
                if (string.IsNullOrEmpty(endpointUrl)) throw new ArgumentNullException(nameof(endpointUrl) + " is null or empty.");
                if (string.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query) + " is null or empty.");

                Uri endpointUri = new(endpointUrl);
                using HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.Authorization = _authenticationHeaderValue;

                SparqlQueryClient sparqlQueryClient = new(httpClient, endpointUri);
                if (SvenSettings.Debug) Debug.Log($"Graph query: {query}");
#if UNITY_WEBGL && !UNITY_EDITOR
                SparqlResultSet results = await sparqlQueryClient.QueryWebGLWithResultSetAsync(query);
#else
                SparqlResultSet results = await sparqlQueryClient.QueryWithResultSetAsync(query);
#endif

                return results;
#if !UNITY_WEBGL || UNITY_EDITOR
            });
#endif
        }

        public static async Task UpdateMemoryAsync(string updateQuery)
        {
            if (string.IsNullOrEmpty(updateQuery)) throw new ArgumentNullException(nameof(updateQuery) + " is null or empty.");

            // Créer une copie du graphe actuel pour travailler dessus
            Graph graphCopy = new();
            lock (_graphLock)
            {
                graphCopy.Assert(_instance.Triples);
                graphCopy.NamespaceMap.Import(_instance.NamespaceMap);
                graphCopy.BaseUri = _instance.BaseUri;
            }

            await Task.Run(() =>
            {
                SparqlUpdateParser parser = new();
                SparqlUpdateCommandSet sparqlUpdate = parser.ParseFromString(updateQuery) ?? throw new InvalidOperationException("Failed to parse SPARQL update query.");
                // Utiliser le dispatcher pour les logs car nous sommes dans un Task.Run

                // Appliquer la mise à jour sur la copie du graphe, SANS VERRO
                TripleStore store = new();
                store.Add(graphCopy, true); // Opère sur la copie
                LeviathanUpdateProcessor processor = new(store, options =>
                {
                    options.UpdateExecutionTimeout = 60 * 1000;
                });
                processor.ProcessCommandSet(sparqlUpdate);

                // Mettre à jour le graphe principal avec les résultats, à l'intérieur d'un verrou court
                lock (_graphLock)
                {
                    _instance.Clear();
                    _instance.Assert(graphCopy.Triples);
                }
            });
        }

        public static async Task<SparqlResultSet> QueryMemoryAsync(string query, bool withInference)
        {
            if (string.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query) + " is null or empty.");

            try
            {
                SparqlResultSet result = await Task.Run(async () =>
                {
                    SparqlQueryParser parser = new();
                    SparqlQuery sparqlQuery = parser.ParseFromString(query) ?? throw new InvalidOperationException("Failed to parse SPARQL query.");
                    if (SvenSettings.Debug) Debug.Log($"Graph query: {query}");

                    if (withInference)
                    {
                        List<Triple> triples;
                        Uri baseUri;
                        NamespaceMapper nsMap;
                        lock (_graphLock)
                        {
                            triples = _instance.Triples.ToList();
                            baseUri = _instance.BaseUri;
                            nsMap = new NamespaceMapper();
                            nsMap.Import(_instance.NamespaceMap);
                        }
                        Graph queryGraph = await GetInferredGraphAsync(triples, baseUri, nsMap);
                        return queryGraph.ExecuteQuery(query) as SparqlResultSet;
                    }
                    else
                    {
                        Graph g = new();
                        lock (_graphLock)
                        {
                            g.NamespaceMap.Import(_instance.NamespaceMap);
                            g.BaseUri = _instance.BaseUri;
                            g.Assert(_instance.Triples);
                        }
                        return g.ExecuteQuery(query) as SparqlResultSet;
                    }
                });
                return result;
            }
            catch (RdfQueryException ex)
            {
                throw new InvalidOperationException($"SPARQL query execution failed: {ex.Message}", ex);
            }
        }

        private static void LoadInstants(SparqlResultSet instantsResultSet)
        {
            _instants.Clear();

            //iterate over the results
            foreach (SparqlResult result in instantsResultSet.Cast<SparqlResult>())
            {
                //get the dateTime
                INode dateTimeNode = result["dateTime"];
                //create a new instant
                DateTimeOffset dateTimeOffset = DateTimeOffset.Parse(dateTimeNode.AsValuedNode().AsString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
                //add the instant to the list
                _instants.Add(new(dateTimeOffset.DateTime));
            }
        }

        private static string LoadInstantsQuery => $@"PREFIX : <{BaseUri}>
PREFIX time: <http://www.w3.org/2006/time#>
PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>

SELECT ?instant ?dateTime (COUNT(?contentModification) as ?contentModifier)
FROM :
WHERE {{
    ?instant a time:Instant ;
            time:inXSDDateTime ?dateTime .
    ?contentModification sven:hasTemporalExtent ?interval .
    ?interval time:hasBeginning ?instant .
}} GROUP BY ?instant ?dateTime ORDER BY ?dateTime";

        public static async Task LoadInstantsFromEndpoint()
        {
            string endpointUrl = SvenSettings.SparqlQueryEndpoint;

            SparqlResultSet results = await QueryEndpoint(endpointUrl, LoadInstantsQuery);
            LoadInstants(results);
        }

        public static string RetrieveIntervalQuery(Instant instant)
        {
            return SvenSettings.UseInside ?
                $"?interval time:inside <{instant.UriNode}> ." :
                $@"
    {{
        SELECT DISTINCT ?interval
        WHERE {{
            VALUES ?instantTime {{ {"\"" + instant.inXSDDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture) + "\""}^^xsd:dateTime }}
            ?interval a time:Interval ;
                    time:hasBeginning/time:inXSDDateTime ?startTime .
            OPTIONAL {{
                ?interval time:hasEnd/time:inXSDDateTime ?_endTime .
            }}
            BIND(IF(BOUND(?_endTime), ?_endTime, NOW()) AS ?endTime)
            FILTER(?startTime <= ?instantTime && ?instantTime < ?endTime)
        }} ORDER BY ?startTime ?endTime limit 10000
    }}";
        }

        private static string RetrieveSceneQuery(Instant instant, bool withFrom)
        {
            return $@"PREFIX : <{BaseUri}>
PREFIX time: <http://www.w3.org/2006/time#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>

SELECT DISTINCT ?object ?component ?componentType ?property ?propertyName ?propertyNestedName ?propertyValue ?propertyType
{(withFrom ? "FROM :" : "")}
WHERE {{
    {{
        VALUES ?propertyName {{
            sven:active
            sven:layer
            sven:tag
            sven:name
        }}
        ?object a sven:VirtualObject ;
                ?propertyName ?property .
        ?property sven:value ?propertyValue ;
                    sven:hasTemporalExtent ?interval .
    }}
    UNION
    {{
        ?object a sven:VirtualObject ;
                sven:component ?component .
        ?component sven:exactType ?componentType ;
                ?propertyName ?property .
        ?propertyName rdfs:subPropertyOf* sven:componentProperty ;
                    rdfs:range ?propertyRange .
        ?property sven:exactType ?propertyType ;
                ?propertyNestedName ?propertyValue ;
                sven:hasTemporalExtent ?interval .
        ?propertyNestedName rdfs:subPropertyOf sven:propertyData .
        FILTER(?propertyNestedName != sven:propertyData)
    }}
    {RetrieveIntervalQuery(instant)}
}}";
        }

        private static async Task<SceneContent> GetSceneContent(SparqlResultSet resultSet)
        {
            SceneContent sceneContent = await Task.Run(() =>
            {
                SceneContent sc = new();

                foreach (SparqlResult result in resultSet.Cast<SparqlResult>())
                {
                    INode objectNode = result["object"];
                    INode propertyNameNode = result["propertyName"];
                    INode propertyValueNode = result["propertyValue"];
                    if (objectNode == null || propertyNameNode == null || propertyValueNode == null) continue;

                    string objectValue = objectNode.ToString();
                    int objectSlashIndex = objectValue.LastIndexOf("/");
                    string objectUUID = objectSlashIndex >= 0 ? objectValue[(objectSlashIndex + 1)..] : objectValue;

                    string propertyName = propertyNameNode.NodeType switch
                    {
                        NodeType.Uri =>
                            propertyNameNode.ToString()[(propertyNameNode.ToString().LastIndexOf("#") + 1)..],
                        _ => propertyNameNode.AsValuedNode().AsString()
                    };

                    string componentUUID, componentStringType, propertyStringType, propertyNestedName;
                    try
                    {
                        INode componentNode = result["component"];
                        INode componentTypeNode = result["componentType"];
                        INode propertyTypeNode = result["propertyType"];
                        INode propertyNestedNameNode = result["propertyNestedName"];
                        if (componentNode == null || componentTypeNode == null || propertyTypeNode == null || propertyNestedNameNode == null)
                            throw new InvalidOperationException();

                        string componentValue = componentNode.ToString();
                        int componentSlashIndex = componentValue.LastIndexOf("/");
                        componentUUID = componentSlashIndex >= 0 ? componentValue[(componentSlashIndex + 1)..] : componentValue;

                        string componentTypeValue = componentTypeNode.ToString();
                        int componentTypeHashIndex = componentTypeValue.LastIndexOf("#");
                        componentStringType = componentTypeHashIndex >= 0 ? componentTypeValue[(componentTypeHashIndex + 1)..] : componentTypeValue;

                        string propertyTypeValue = propertyTypeNode.ToString();
                        int propertyTypeHashIndex = propertyTypeValue.LastIndexOf("#");
                        propertyStringType = propertyTypeHashIndex >= 0 ? propertyTypeValue[(propertyTypeHashIndex + 1)..] : propertyTypeValue;

                        string propertyNestedValue = propertyNestedNameNode.ToString();
                        int propertyNestedHashIndex = propertyNestedValue.LastIndexOf("#");
                        propertyNestedName = propertyNestedHashIndex >= 0 ? propertyNestedValue[(propertyNestedHashIndex + 1)..] : propertyNestedValue;
                    }
                    catch
                    {
                        if (!sc.GameObjects.ContainsKey(objectUUID))
                            sc.GameObjects[objectUUID] = new(objectUUID);

                        switch (propertyName)
                        {
                            case "active":
                                sc.GameObjects[objectUUID].Active = propertyValueNode.AsValuedNode().AsString() == "true";
                                continue;
                            case "layer":
                                sc.GameObjects[objectUUID].Layer = propertyValueNode.AsValuedNode().AsString();
                                continue;
                            case "tag":
                                sc.GameObjects[objectUUID].Tag = propertyValueNode.AsValuedNode().AsString();
                                continue;
                            case "name":
                                sc.GameObjects[objectUUID].Name = propertyValueNode.AsValuedNode().AsString();
                                continue;
                        }
                        continue;
                    }

                    Tuple<Type, int> componentData = MapppedComponents.GetData(componentStringType);
                    if (componentData == null) continue;

                    Type componentType = componentData.Item1;
                    int componentSortOrder = componentData.Item2;
                    if (componentType == null || !MapppedComponents.HasProperty(componentType, propertyName)) continue;

                    Type propertyType = MapppedProperties.GetType(propertyStringType) ?? Type.GetType(propertyStringType);
                    if (propertyType == null || !MapppedProperties.HasNestedProperty(propertyType, propertyNestedName)) continue;

                    INode propertyNode = result["property"];
                    if (propertyNode == null) continue;

                    string propertyString = propertyNode.ToString();
                    int propertySlashIndex = propertyString.LastIndexOf("/");
                    string propertyUUID = propertySlashIndex >= 0 ? propertyString[(propertySlashIndex + 1)..] : propertyString;
                    object propertyValue = propertyValueNode.AsValuedNode().ToValue();

                    if (!sc.GameObjects.ContainsKey(objectUUID))
                        sc.GameObjects[objectUUID] = new(objectUUID);

                    if (!sc.GameObjects[objectUUID].Components.ContainsKey(componentUUID))
                        sc.GameObjects[objectUUID].Components[componentUUID] = new(componentUUID, componentType, componentSortOrder);

                    if (!sc.GameObjects[objectUUID].Components[componentUUID].Properties.ContainsKey(propertyName))
                        sc.GameObjects[objectUUID].Components[componentUUID].Properties[propertyName] = new(propertyUUID, propertyName, propertyType);

                    if (!sc.GameObjects[objectUUID].Components[componentUUID].Properties[propertyName].Values.ContainsKey(propertyNestedName))
                        sc.GameObjects[objectUUID].Components[componentUUID].Properties[propertyName].Values[propertyNestedName] = propertyValue;
                    else Debug.LogWarning($"Property {propertyNestedName} already exists in {propertyName} of {componentType} in {objectUUID}");
                }
                return sc;
            });
            await Task.Yield();

            return sceneContent;
        }

        /// <summary>
        /// Search the instant that is closer previous the duration sent.
        /// </summary>
        /// <param name="duration">Duration to search.</param>
        public static Instant SearchInstant(float duration)
        {
            Instant searchedInstant = _instants.LastOrDefault(x => x.inXSDDateTime <= StartedAt.AddSeconds(duration));
            return searchedInstant;
        }


        public static Instant SearchInstant(DateTime date)
        {
            Instant searchedInstant = _instants.LastOrDefault(x => x.inXSDDateTime <= date);
            if (searchedInstant == null)
            {
                Debug.LogWarning($"No instant found for date {date}. Returning the first instant.");
                searchedInstant = _instants.FirstOrDefault();
            }
            return searchedInstant;
        }

        public static async Task RetrieveSceneFromEndpoint(Instant instant)
        {
            CurrentInstantLoaded = instant;
            if (instant == null) return;
            string endpointUrl = SvenSettings.SparqlQueryEndpoint;

            SparqlResultSet results = await QueryEndpoint(endpointUrl, RetrieveSceneQuery(instant, true));
            SceneContent targetSceneContent = await GetSceneContent(results);
            ReconstructScene(targetSceneContent);
        }

        public static async Task RetrieveSceneFromMemory(Instant instant)
        {
            CurrentInstantLoaded = instant;
            if (instant == null) return;

            SparqlResultSet results = await QueryMemoryAsync(RetrieveSceneQuery(instant, false), false);
            SceneContent targetSceneContent = await GetSceneContent(results);
            ReconstructScene(targetSceneContent);
        }

        private static SceneContent GetSceneContent()
        {
            try
            {
                SceneContent sceneContent = new(CurrentInstant);
                // get all semantizationCore objects in the scene
                SemantizationCore[] semantizationCores = UnityEngine.Object.FindObjectsByType<SemantizationCore>();
                // iterate over the semantizationCores and get their content
                // do the things to fill SceneContent
                foreach (SemantizationCore semantizationCore in semantizationCores)
                {
                    string objectUUID = semantizationCore.GetUUID();
                    if (!sceneContent.GameObjects.ContainsKey(objectUUID))
                    {
                        GameObjectDescription gameObjectDescription = new(objectUUID)
                        {
                            Active = semantizationCore.gameObject.activeSelf,
                            Layer = LayerMask.LayerToName(semantizationCore.gameObject.layer),
                            Tag = semantizationCore.gameObject.tag,
                            Name = semantizationCore.gameObject.name,
                            GameObject = semantizationCore.gameObject
                        };
                        sceneContent.GameObjects[objectUUID] = gameObjectDescription;
                    }

                    List<Component> components = semantizationCore.GetComponents<Component>().ToList();
                    foreach (Component component in components)
                    {
                        string componentUUID = component.GetUUID();
                        if (!sceneContent.GameObjects[objectUUID].Components.ContainsKey(componentUUID))
                        {
                            Tuple<Type, int> componentData = MapppedComponents.GetData(component.GetRdfType());
                            if (componentData == null) continue;
                            Type componentType = componentData.Item1;
                            int componentSortOrder = componentData.Item2;

                            if (!sceneContent.GameObjects[objectUUID].Components.ContainsKey(componentUUID))
                                sceneContent.GameObjects[objectUUID].Components[componentUUID] = new(componentUUID, componentType, componentSortOrder)
                                {
                                    Component = component
                                };

                            Dictionary<string, Tuple<int, Func<object>>> getters = MapppedComponents.GetGetters(component);
                            foreach (KeyValuePair<string, Tuple<int, Func<object>>> getter in getters)
                            {
                                string propertyName = getter.Key;
                                Func<object> getterFunc = getter.Value.Item2;
                                object propertyValue = getterFunc();
                                if (propertyValue == null) continue;
                                Type propertyType = propertyValue.GetType();
                                if (!sceneContent.GameObjects[objectUUID].Components[componentUUID].Properties.ContainsKey(propertyName))
                                    sceneContent.GameObjects[objectUUID].Components[componentUUID].Properties[propertyName] = new(propertyName, propertyName, propertyType);

                                List<string> nestedProperties = MapppedProperties.GetNestedProperties(propertyValue.GetType());
                                foreach (string nestedProperty in nestedProperties)
                                {
                                    object nestedValue;
                                    if (nestedProperty == "value")
                                    {
                                        nestedValue = propertyValue;
                                    }
                                    else
                                    {
                                        nestedValue = propertyType.GetField(nestedProperty)?.GetValue(propertyValue) ??
                                                      propertyType.GetProperty(nestedProperty)?.GetValue(propertyValue);
                                    }
                                    if (!sceneContent.GameObjects[objectUUID].Components[componentUUID].Properties[propertyName].Values.ContainsKey(nestedProperty))
                                        sceneContent.GameObjects[objectUUID].Components[componentUUID].Properties[propertyName].Values[nestedProperty] = nestedValue;
                                }
                            }
                        }
                    }
                }
                return sceneContent;
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                return null;
            }
        }

        private static void ReconstructScene(SceneContent sceneContent)
        {
            SceneContent currentSceneContent = GetSceneContent();
            try
            {
                if (SvenSettings.Debug) Debug.Log(currentSceneContent);
                if (SvenSettings.Debug) Debug.Log(sceneContent);

                foreach (GameObjectDescription gameObjectDescription in sceneContent.GameObjects.Values)
                {
#if UNITY_EDITOR
                    if (!EditorApplication.isPlaying) return;
#endif
                    // create gamobject if it doesn't exist, otherwise get it from the current scene content
                    bool gameObjectExist = currentSceneContent.GameObjects.ContainsKey(gameObjectDescription.UUID);
                    if (gameObjectExist)
                        gameObjectDescription.GameObject = currentSceneContent.GameObjects[gameObjectDescription.UUID].GameObject;
                    else
                    {
                        gameObjectDescription.GameObject = new GameObject(gameObjectDescription.UUID);
                        gameObjectDescription.GameObject.AddComponent<SemantizationCore>().AddUUID(gameObjectDescription.UUID);
                        //gameObjectDescription.GameObject.transform.SetParent(transform);
                    }
                    gameObjectDescription.GameObject.SetActive(gameObjectDescription.Active);
                    // NameToLayer renvoie -1 si le layer n'existe pas dans le projet de relecture (les layers sont
                    // propres à chaque projet). On garde alors le layer par défaut au lieu de faire échouer tout l'instant.
                    int replayLayer = string.IsNullOrEmpty(gameObjectDescription.Layer) ? -1 : LayerMask.NameToLayer(gameObjectDescription.Layer);
                    if (replayLayer >= 0) gameObjectDescription.GameObject.layer = replayLayer;
                    else if (SvenSettings.Debug) Debug.LogWarning($"Replay: layer '{gameObjectDescription.Layer}' introuvable dans le projet de relecture, layer par défaut conservé.");
                    try
                    {
                        bool isTagExist = !string.IsNullOrEmpty(gameObjectDescription.Tag);
                        gameObjectDescription.GameObject.tag = isTagExist ? gameObjectDescription.Tag ?? "Untagged" : "Untagged";
                    }
                    catch (Exception)
                    {
                        gameObjectDescription.GameObject.tag = "Untagged";
                    }
                    gameObjectDescription.GameObject.name = gameObjectDescription.Name;

                    List<ComponentDescription> componentDescriptions = gameObjectDescription.Components.Values.ToList();
                    // sort the components by sort order
                    componentDescriptions = componentDescriptions.OrderBy(x => x.SortOrder).ToList();

                    foreach (ComponentDescription componentDescription in componentDescriptions)
                    {
                        // create component if it doesn't exist, otherwise get it from the current scene content
                        bool componentExist = gameObjectExist && currentSceneContent.GameObjects[gameObjectDescription.UUID].Components.ContainsKey(componentDescription.UUID);
                        if (componentExist)
                            componentDescription.Component = currentSceneContent.GameObjects[gameObjectDescription.UUID].Components[componentDescription.UUID].Component;
                        else
                        {
                            // we check transform because it is a special case, it is already attached to the gameObject at instantiation and is unique
                            if (componentDescription.Type == typeof(Transform))
                            {
                                componentDescription.Component = gameObjectDescription.GameObject.transform;
                            }
                            else
                            {
                                try
                                {
                                    //Debug.Log(componentDescription.Type);
                                    componentDescription.Component = gameObjectDescription.GameObject.AddComponent(componentDescription.Type);

                                    // Default initialization
                                    if (componentDescription.Component is MeshRenderer meshRenderer)
                                        meshRenderer.material = new Material(Shader.Find("Standard"));
                                    if (componentDescription.Component is MeshFilter meshFilter)
                                        meshFilter.mesh = new Mesh();
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogError($"Error while adding component {componentDescription.Type} to {gameObjectDescription.UUID}: {ex.Message}");
                                }
                            }
                            componentDescription.Component.AddUUID(componentDescription.UUID);
                        }

                        // get the setters of the component item1 = priority, item2 = setter
                        Dictionary<string, Tuple<int, Action<object>>> setters = MapppedComponents.GetSetters(componentDescription.Component);
                        //reorder the properties by priority
                        componentDescription.Properties = componentDescription.Properties.OrderBy(x => setters[x.Key].Item1).ToDictionary(x => x.Key, x => x.Value);
                        //if (componentDescription.Component.GetType() == typeof(MeshFilter)) continue;

                        foreach (PropertyDescription propertyDescription in componentDescription.Properties.Values)
                        {
                            // check if the property state exists in the current scene content (by checking the uuid)
                            bool propertyExist = componentExist && currentSceneContent.GameObjects[gameObjectDescription.UUID].Components[componentDescription.UUID].Properties.TryGetValue(propertyDescription.Name, out var currentPropertyDescription) && currentPropertyDescription.UUID == propertyDescription.UUID;
                            if (propertyExist) continue;

                            object propertyValue = propertyDescription.Value;
                            if (propertyValue == null)
                            {
                                Debug.LogWarning($"Property {propertyDescription} is null in {componentDescription.Type} of {gameObjectDescription.UUID}");
                                continue;
                            }
                            //if (componentDescription.Component.GetType() == typeof(MeshRenderer))
                            //    Debug.Log($"Property: {propertyDescription.Name} {propertyDescription.Type} {propertyValue.GetType()}  {propertyValue}");

                            if (setters.TryGetValue(propertyDescription.Name, out var setter) && setter.Item2 != null)
                            {
                                try
                                {
                                    setter.Item2(propertyValue);
                                }
                                catch (Exception e)
                                {
                                    Debug.LogWarning($"Info: Could not set property {propertyDescription.Name} of type {propertyDescription.Type} in {componentDescription.Type} of {gameObjectDescription.UUID}: {e}");
                                }
                            }
                            //else Debug.LogWarning($"Setter not found for {propertyDescription.Type} in {componentDescription.Type} of {gameObjectDescription.UUID}");
                        }
                    }
                }

                // remove the gameobjects and components that are not in the target scene content
                foreach (GameObjectDescription gameObjectDescription in currentSceneContent.GameObjects.Values)
                {
                    if (!sceneContent.GameObjects.ContainsKey(gameObjectDescription.UUID))
                    {
                        foreach (ComponentDescription componentDescription in gameObjectDescription.Components.Values)
                        {
                            Tween.StopAll(componentDescription.Component);
                            if (componentDescription.Type != typeof(Transform))
                                GameObject.Destroy(componentDescription.Component);
                        }
                        GameObject.Destroy(gameObjectDescription.GameObject);
                    }
                    else
                    {
                        foreach (ComponentDescription componentDescription in gameObjectDescription.Components.Values)
                            if (!sceneContent.GameObjects[gameObjectDescription.UUID].Components.ContainsKey(componentDescription.UUID))
                            {
                                Tween.StopAll(componentDescription.Component);
                                if (componentDescription.Type != typeof(Transform))
                                    GameObject.Destroy(componentDescription.Component);
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        public static Instant NextInstant()
        {
            if (CurrentInstantLoaded == null) return _instants.Count > 0 ? _instants.First() : CurrentInstantLoaded;

            // next of current instant
            Instant nextInstant = _instants.FirstOrDefault(x => x.inXSDDateTime > CurrentInstantLoaded.inXSDDateTime);
            if (nextInstant != null) return nextInstant;
            // if no next instant, return the current instant
            Debug.LogWarning("No next instant found, returning current instant instead.");
            return CurrentInstantLoaded;
        }

        public static Instant PreviousInstant()
        {
            if (CurrentInstantLoaded == null) return _instants.Count > 0 ? _instants.First() : CurrentInstantLoaded;

            // previous of current instant
            Instant previousInstant = _instants.LastOrDefault(x => x.inXSDDateTime < CurrentInstantLoaded.inXSDDateTime);
            if (previousInstant != null) return previousInstant;
            // if no previous instant, return the current instant
            Debug.LogWarning("No previous instant found, returning current instant instead.");
            return CurrentInstantLoaded;
        }

        public static async Task<string> DownloadTTLFromEndpoint(string enpointUrl)
        {

            string query = $@"PREFIX : <{BaseUri}>
PREFIX time: <http://www.w3.org/2006/time#>
PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX geo: <http://www.opengis.net/ont/geosparql#>

CONSTRUCT {{
    ?s ?p ?o .
}}
FROM :
WHERE {{
    ?s ?p ?o .
}}";

            Uri endpointUri = new(enpointUrl);
            using HttpClient httpClient = new();

            httpClient.DefaultRequestHeaders.Authorization = _authenticationHeaderValue;

            SparqlQueryClient sparqlQueryClient = new(httpClient, endpointUri);
#if UNITY_WEBGL && !UNITY_EDITOR
            string ttlContent = await sparqlQueryClient.QueryWebGLWithResultTTLAsync(query);
#else
            IGraph resultGraph = await sparqlQueryClient.QueryWithResultGraphAsync(query);
            foreach (string prefix in _instance.NamespaceMap.Prefixes)
            {
                Uri uri = _instance.NamespaceMap.GetNamespaceUri(prefix);
                resultGraph.NamespaceMap.AddNamespace(prefix, uri);
            }

            string ttlContent = DecodeGraph(resultGraph);
#endif
            return ttlContent;
        }


        private static readonly List<ProcessingData> processedDatas = new();
        private class ProcessingData
        {
            public double queryTime;
            public double sceneUpdateTime;
            public double totalProcessingTime;
            public override string ToString()
            {
                return $"Query Time: {queryTime} ms\nScene Update Time: {sceneUpdateTime} ms\nTotal Processing Time: {totalProcessingTime} ms";
            }
        }

        public static void PrintExperimentResults()
        {
            if (processedDatas.Count == 0)
            {
                Debug.Log("No experiment data to process.");
                return;
            }
            string results = "";

            results += "SPARQL-Sampled: " + processedDatas.Count + "\n";

            results += "SPARQL-Median: " + processedDatas.OrderBy(x => x.queryTime).ElementAt(processedDatas.Count / 2).queryTime + " ms\n";
            results += "SPARQL-Mean: " + processedDatas.Average(x => x.queryTime) + " ms\n";
            results += "SPARQL-Min: " + processedDatas.Min(x => x.queryTime) + " ms\n";
            results += "SPARQL-Max: " + processedDatas.Max(x => x.queryTime) + " ms\n";

            results += "SceneUpdate-Median: " + processedDatas.OrderBy(x => x.sceneUpdateTime).ElementAt(processedDatas.Count / 2).sceneUpdateTime + " ms\n";
            results += "SceneUpdate-Mean: " + processedDatas.Average(x => x.sceneUpdateTime) + " ms\n";
            results += "SceneUpdate-Min: " + processedDatas.Min(x => x.sceneUpdateTime) + " ms\n";
            results += "SceneUpdate-Max: " + processedDatas.Max(x => x.sceneUpdateTime) + " ms\n";

            results += "TotalProcessing-Median: " + processedDatas.OrderBy(x => x.totalProcessingTime).ElementAt(processedDatas.Count / 2).totalProcessingTime + " ms\n";
            results += "TotalProcessing-Mean: " + processedDatas.Average(x => x.totalProcessingTime) + " ms\n";
            results += "TotalProcessing-Min: " + processedDatas.Min(x => x.totalProcessingTime) + " ms\n";
            results += "TotalProcessing-Max: " + processedDatas.Max(x => x.totalProcessingTime) + " ms\n";

            Debug.Log("Experiment results:\n" + results);
        }

        public static async Task SynchronizeAsync()
        {
            await RetrieveSceneFromEndpoint(new Instant(DateTime.Now));
        }

        public static Graph InstanceCopy()
        {
            Graph g = new();
            lock (_graphLock)
            {
                g.NamespaceMap.Import(_instance.NamespaceMap);
                g.BaseUri = _instance.BaseUri;
                g.Assert(_instance.Triples);
            }
            return g;
        }
    }

    public static class TaskExtensions
    {
        /// <summary>
        /// Executes a task without awaiting it, handling any exceptions that may occur.
        /// </summary>
        /// <param name="task">The task to execute.</param>
        public static void FireAndForget(this Task task)
        {
            // Lance la tâche sur un thread d'arrière-plan et oublie-la.
            Task.Run(async () =>
            {
                try
                {
                    // Attend la fin de la tâche sans tenter de revenir au contexte original.
                    await task.ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // Si une erreur se produit, la logger sur le thread principal pour qu'elle soit visible dans la console Unity.
                    if (UnityMainThreadDispatcher.IsMainThread)
                    {
                        Debug.LogError($"Exception in FireAndForget task: {e}");
                    }
                    else
                    {
                        Debug.LogError($"Exception in FireAndForget task: {e}");
                    }
                }
            });
        }
    }
}
