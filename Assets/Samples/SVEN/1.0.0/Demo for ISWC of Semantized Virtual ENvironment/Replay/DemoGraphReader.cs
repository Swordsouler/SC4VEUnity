using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Sven.Content;
using Sven.GraphManagement;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Query;

namespace Sven.Demo
{
    public class DemoGraphReader : GraphReader
    {
        public new void Awake()
        {
            readingMode = GraphStorageMode.Remote;
            graphName = DemoManager.graphName;
            endpoint = DemoManager.EndpointUri.ToString();
            MapppedComponents.AddComponentDescription(typeof(DemoSprayController), new("Spray",
                new List<Delegate>
                {
                    (Func<DemoSprayController, MapppedComponents.PropertyDescription>)(spray => new MapppedComponents.PropertyDescription("enabled", () => spray.enabled, value => spray.enabled = value.ToString() == "true", 1)),
                }, 1));
            base.Awake();
        }

        public async Task<IGraph> GetGraph()
        {
            string query = $@"CONSTRUCT {{
    ?s ?p ?o .
}}
FROM <{graph.BaseUri.AbsoluteUri}{Uri.EscapeDataString(graphName)}>
WHERE {{
    ?s ?p ?o .
}}";

            Uri endpointUri = new(_loadedEndpoint);
            HttpClient httpClient = new();
            SparqlQueryClient sparqlQueryClient = new(httpClient, endpointUri);
#if UNITY_WEBGL && !UNITY_EDITOR
            IGraph resultGraph = await sparqlQueryClient.QueryWebGLWithResultGraphAsync(query);
#else
            IGraph resultGraph = await sparqlQueryClient.QueryWithResultGraphAsync(query);
#endif
            //resultGraph.NamespaceMap = graph.NamespaceMap;
            foreach (GraphNamespace graphNamespace in ontologyDescription.Namespaces)
            {
                if (!resultGraph.NamespaceMap.HasNamespace(graphNamespace.Name))
                    resultGraph.NamespaceMap.AddNamespace(graphNamespace.Name, new Uri(graphNamespace.Uri));
            }

            return resultGraph;
        }
    }
}
