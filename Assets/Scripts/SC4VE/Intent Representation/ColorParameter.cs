using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;

namespace Sc4ve.Multimodality
{
    public class Color
    {
        [SerializeField] private float _red;
        public float Red
        {
            get => _red;
            set => _red = value;
        }
        [SerializeField] private float _green;
        public float Green
        {
            get => _green;
            set => _green = value;
        }
        [SerializeField] private float _blue;
        public float Blue
        {
            get => _blue;
            set => _blue = value;
        }
        [SerializeField] private float _tolerance;
        public float Tolerance
        {
            get => _tolerance;
            set => _tolerance = value;
        }

        public float MinRed => Mathf.Clamp(Red - Tolerance, 0f, 1f);
        public float MaxRed => Mathf.Clamp(Red + Tolerance, 0f, 1f);
        public float MinGreen => Mathf.Clamp(Green - Tolerance, 0f, 1f);
        public float MaxGreen => Mathf.Clamp(Green + Tolerance, 0f, 1f);
        public float MinBlue => Mathf.Clamp(Blue - Tolerance, 0f, 1f);
        public float MaxBlue => Mathf.Clamp(Blue + Tolerance, 0f, 1f);
    }

    public class ColorParameter : Parameter
    {
        [SerializeField] private string _value;
        [JsonProperty("value")]
        public string Value
        {
            get => _value;
            set => _value = value;
        }

        [SerializeField] private DateTime? _timestamp;
        [JsonProperty("timestamp")]
        public DateTime? Timestamp
        {
            get => _timestamp;
            set => _timestamp = value;
        }

        public async Task<Color> QueryColor(Graph queryGraph)
        {
            string locale = MultimodalityController.LoadedLocale;
            // execute sparql query to get color from value
            string query = $@"
PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX sc4ve: <https://sc4ve.lisn.upsaclay.fr/ontology#>

SELECT ?r ?g ?b ?t
WHERE {{
    ?color a sven:Color ;
           sven:label ""{Value}""@{locale} ;
           sven:r ?r ;
           sven:g ?g ;
           sven:b ?b ;
           sc4ve:tolerance ?t .
}}";
            return null;
            // execute query and parse result
            //SparqlResultSet results = await queryGraph.ExecuteQuery(query) as SparqlResultSet;

        }

        public override async Task<IUriNode> Semanticize(Graph graph)
        {
            IUriNode parameterNode = await base.Semanticize(graph);

            Color color = await QueryColor(graph);
            if (color != null)
            {
                IUriNode r = graph.CreateUriNode("sven:r");
                IUriNode g = graph.CreateUriNode("sven:g");
                IUriNode b = graph.CreateUriNode("sven:b");
                IUriNode tolerance = graph.CreateUriNode("sc4ve:tolerance");
                // insert triples for color components (0 to 1)
                graph.Assert(new Triple(parameterNode, r, graph.CreateLiteralNode(color.Red.ToString(), graph.CreateUriNode("xsd:float").Uri)));
                graph.Assert(new Triple(parameterNode, g, graph.CreateLiteralNode(color.Green.ToString(), graph.CreateUriNode("xsd:float").Uri)));
                graph.Assert(new Triple(parameterNode, b, graph.CreateLiteralNode(color.Blue.ToString(), graph.CreateUriNode("xsd:float").Uri)));
                graph.Assert(new Triple(parameterNode, tolerance, graph.CreateLiteralNode(color.Tolerance.ToString(), graph.CreateUriNode("xsd:float").Uri)));
            }
            return parameterNode;
        }
    }
}