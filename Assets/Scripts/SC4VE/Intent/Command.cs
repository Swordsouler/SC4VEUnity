using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;

namespace Sc4ve.Multimodality.Intent
{
    [JsonConverter(typeof(CommandConverter))]
    [Serializable]
    public abstract class Command
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
            IUriNode rdfType = graph.CreateUriNode("rdf:type");
            IUriNode commandType = graph.CreateUriNode($"sc4ve:{Type}");
            // Add the type triple
            graph.Assert(new Triple(commandNode, rdfType, commandType));
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

        public abstract void Execute();

        protected T GetParameter<T>() where T : Parameter
        {
            return Parameters?.OfType<T>().FirstOrDefault();
        }
    }
}