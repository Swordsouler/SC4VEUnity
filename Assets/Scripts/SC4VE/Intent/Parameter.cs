using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;

namespace Sc4ve.Multimodality.Parameter
{
    [JsonConverter(typeof(ParameterConverter))]
    [Serializable]
    public class Parameter
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

        public virtual async Task<IUriNode> Semanticize(Graph graph)
        {
            IUriNode parameterNode = graph.CreateUriNode($"ve:{Id}");
            IUriNode rdfType = graph.CreateUriNode("rdf:type");
            IUriNode parameterType = graph.CreateUriNode($"sc4ve:{Type}");
            // Add the type triple
            graph.Assert(new Triple(parameterNode, rdfType, parameterType));
            return parameterNode;
        }
    }
}