// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if !(!UNITY_WEBGL || UNITY_EDITOR)
using System.IO;
using VDS.RDF.Parsing;
using System.Threading;
#endif
using Sven.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using VDS.RDF;
using VDS.RDF.Query;

namespace Sven.GraphManagement
{
    public static class DotNetRdfWebGL
    {
        public static async Task<SparqlResultSet> QueryWebGLWithResultSetAsync(this SparqlQueryClient client, string query)
        {
#if !(!UNITY_WEBGL || UNITY_EDITOR)
            TaskCompletionSource<SparqlResultSet> tcs = new();
            UnityMainThreadDispatcher.Instance.Enqueue(async () =>
            {
                try
                {
                    using UnityWebRequest request = UnityWebRequest.Post(client.EndpointUri, new Dictionary<string, string>
                    {
                        { "query", query },
                        { "format", "application/sparql-results+json" }
                    });

                    request.SetRequestHeader("Accept", "application/sparql-results+json");
                    string auth = GraphManager.AuthorizationHeader;
                    if (!string.IsNullOrEmpty(auth)) request.SetRequestHeader("Authorization", auth);

                    await request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string jsonResponse = request.downloadHandler.text;

                        SparqlResultSet resultSet = new();
                        SparqlJsonParser parser = new();
                        using StringReader reader = new(jsonResponse);
                        parser.Load(resultSet, reader);

                        tcs.SetResult(resultSet);
                    }
                    else
                    {
                        Debug.LogError($"Erreur lors de la requête SPARQL : {request.error}");
                        tcs.SetException(new Exception($"SPARQL query failed: {request.error}"));
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception dans QueryWebGLWithResultSetAsync : {ex}");
                    tcs.SetException(ex);
                }
            });

            SparqlResultSet resultSet = await tcs.Task;

            //foreach (SparqlResult result in resultSet.Cast<SparqlResult>()) Debug.Log($"Result: {result.ToString()}");

            return resultSet;
#else
            return await client.QueryWithResultSetAsync(query);
#endif
        }



        public static async Task<string> QueryWebGLWithResultTTLAsync(this SparqlQueryClient client, string query)
        {
            TaskCompletionSource<string> tcs = new();
            UnityMainThreadDispatcher.Instance.Enqueue(async () =>
            {
                try
                {
                    using UnityWebRequest request = UnityWebRequest.Post(client.EndpointUri, new Dictionary<string, string>
                    {
                        { "query", query },
                        { "format","text/turtle" }
                    });

                    request.SetRequestHeader("Accept", "text/turtle");
                    string auth = GraphManager.AuthorizationHeader;
                    if (!string.IsNullOrEmpty(auth)) request.SetRequestHeader("Authorization", auth);

                    await request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        tcs.SetResult(request.downloadHandler.text);
                    }
                    else
                    {
                        Debug.LogError($"SPARQL query failed: {request.error}");
                        Debug.LogError($"Response: {request.downloadHandler.text}");
                        tcs.SetException(new Exception($"SPARQL query failed: {request.error}"));
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception dans QueryWebGLWithResultSetAsync : {ex}");
                    tcs.SetException(ex);
                }
            });

            return await tcs.Task;
        }

        // returns a graph from the query result (CONSTRUCT query)
        public static async Task<IGraph> QueryWebGLWithResultGraphAsync(this SparqlQueryClient client, string query)
        {
#if !(!UNITY_WEBGL || UNITY_EDITOR)
            TaskCompletionSource<IGraph> tcs = new();
            UnityMainThreadDispatcher.Instance.Enqueue(async () =>
            {
                try
                {
                    using UnityWebRequest request = UnityWebRequest.Post(client.EndpointUri, new Dictionary<string, string>
                    {
                        { "query", query },
                        { "format","text/turtle" }
                    });

                    request.SetRequestHeader("Accept", "text/turtle");
                    string auth = GraphManager.AuthorizationHeader;
                    if (!string.IsNullOrEmpty(auth)) request.SetRequestHeader("Authorization", auth);

                    await request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string responseText = request.downloadHandler.text;
                        Debug.Log($"Response size: {responseText.Length} characters");
                        // 1000 characters is a good size for a response
                        Debug.Log($"Response: {responseText.Substring(0, Math.Min(1000, responseText.Length))}");

                        IGraph resultGraph = new Graph();
                        IRdfReader rdfParser = MimeTypesHelper.GetParser("text/turtle");
                        using StringReader reader = new(responseText);
                        rdfParser.Load(resultGraph, reader);


                        tcs.SetResult(resultGraph);
                    }
                    else
                    {
                        Debug.LogError($"SPARQL query failed: {request.error}");
                        Debug.LogError($"Response: {request.downloadHandler.text}");
                        tcs.SetException(new Exception($"SPARQL query failed: {request.error}"));
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception dans QueryWebGLWithResultSetAsync : {ex}");
                    tcs.SetException(ex);
                }
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                Task timeoutTask = Task.Delay(Timeout.Infinite, cts.Token);
                Task<IGraph> graphTask = tcs.Task;

                Task completedTask = await Task.WhenAny(graphTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Debug.LogError("SPARQL query timed out.");
                    throw new OperationCanceledException("The operation timed out.");
                }

                IGraph resultGraph = await graphTask; // La tâche s'est terminée avant le délai
                Debug.Log($"Graph loaded: {resultGraph.Triples.Count} triples");
                return resultGraph;
            }
            catch (OperationCanceledException)
            {
                Debug.LogError("SPARQL query timed out.");
                throw;
            }
#else
            return await client.QueryWithResultGraphAsync(query);
#endif
        }
    }
}