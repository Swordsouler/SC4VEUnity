using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Query;

namespace Sc4ve.Multimodality.Parameter
{
    [Serializable]
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
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

SELECT ?r ?g ?b ?t
WHERE {{
    ?color a sven:Color ;
           rdfs:label ""{Value}""@{locale} ;
           sven:r ?r ;
           sven:g ?g ;
           sven:b ?b ;
           sc4ve:tolerance ?t .
}}";
            // execute query and parse result
            // extract color components from first result
            if (queryGraph.ExecuteQuery(query) is SparqlResultSet results && results.Count > 0)
            {
                SparqlResult result = (SparqlResult)results.Results[0];

                // Use ILiteralNode.Value and invariant culture parsing to avoid formatting issues

                if (result["r"] is not ILiteralNode rNode ||
                    result["g"] is not ILiteralNode gNode ||
                    result["b"] is not ILiteralNode bNode ||
                    result["t"] is not ILiteralNode tNode)
                {
                    Debug.LogWarning("QueryColor: one or more color components are not literal nodes.");
                    return null;
                }

                // Parse strictly with invariant culture so decimal separator is '.'
                bool okR = float.TryParse(rNode.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float rVal);
                bool okG = float.TryParse(gNode.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float gVal);
                bool okB = float.TryParse(bNode.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float bVal);
                bool okT = float.TryParse(tNode.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float tVal);

                if (!okR || !okG || !okB || !okT)
                {
                    Debug.LogWarning($"QueryColor: failed to parse color components. r='{rNode.Value}', g='{gNode.Value}', b='{bNode.Value}', t='{tNode.Value}'");
                    return null;
                }

                return new Color
                {
                    Red = rVal,
                    Green = gVal,
                    Blue = bVal,
                    Tolerance = tVal
                };
            }
            else
            {
                return null;
            }
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
                // insert triples for color components (0 to 1) — use InvariantCulture so ToString uses '.'
                graph.Assert(new Triple(parameterNode, r, graph.CreateLiteralNode(color.Red.ToString(CultureInfo.InvariantCulture), graph.CreateUriNode("xsd:float").Uri)));
                graph.Assert(new Triple(parameterNode, g, graph.CreateLiteralNode(color.Green.ToString(CultureInfo.InvariantCulture), graph.CreateUriNode("xsd:float").Uri)));
                graph.Assert(new Triple(parameterNode, b, graph.CreateLiteralNode(color.Blue.ToString(CultureInfo.InvariantCulture), graph.CreateUriNode("xsd:float").Uri)));
                graph.Assert(new Triple(parameterNode, tolerance, graph.CreateLiteralNode(color.Tolerance.ToString(CultureInfo.InvariantCulture), graph.CreateUriNode("xsd:float").Uri)));
            }
            return parameterNode;
        }
    }
}