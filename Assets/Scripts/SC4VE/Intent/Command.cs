using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;

namespace Sc4ve.Multimodality.Parameter
{
    [Serializable]
    public class Command
    {
        [SerializeField] private string _id = string.Empty;
        public string Id
        {
            get
            {
                if (string.IsNullOrEmpty(_id))
                    _id = Guid.NewGuid().ToString();
                return _id;
            }
        }

        [SerializeField] private string _type;
        [JsonProperty("type")]
        public string Type
        {
            get => _type;
            set => _type = value;
        }

        [SerializeField] private List<Parameter> _parameters;
        [JsonProperty("parameters")]
        public List<Parameter> Parameters
        {
            get => _parameters;
            set => _parameters = value;
        }

        public async Task<IUriNode> Semanticize(Graph graph)
        {
            // Create a URI node for the command
            IUriNode commandNode = graph.CreateUriNode($":{Id}");
            IUriNode a = graph.CreateUriNode("a");
            IUriNode commandType = graph.CreateUriNode($"sc4ve:{Type}");
            // Add the type triple
            graph.Assert(new Triple(commandNode, a, commandType));
            // Add parameters
            if (Parameters != null)
            {
                IUriNode hasParameter = graph.CreateUriNode($"sc4ve:hasParameter");
                foreach (Parameter parameter in Parameters)
                {
                    IUriNode parameterNode = await parameter.Semanticize(graph);
                    graph.Assert(new Triple(commandNode, hasParameter, parameterNode));
                }
            }
            return commandNode;
        }
    }
}