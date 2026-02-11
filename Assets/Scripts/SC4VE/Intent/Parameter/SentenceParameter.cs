using Newtonsoft.Json;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;

namespace Sc4ve.Multimodality.Intent
{
    public class SentenceParameter : Parameter
    {
        [SerializeField] private string _value;
        [JsonProperty("value")]
        public string Value
        {
            get => _value;
            set => _value = value;
        }

        public override async Task<IUriNode> Semanticize(Graph graph)
        {
            IUriNode parameterNode = await base.Semanticize(graph);
            IUriNode value = graph.CreateUriNode("sven:value");
            graph.Assert(new Triple(parameterNode, value, graph.CreateLiteralNode(Value, graph.CreateUriNode("xsd:string").Uri)));
            return parameterNode;
        }
    }
}