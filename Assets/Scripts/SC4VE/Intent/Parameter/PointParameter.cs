using Newtonsoft.Json;
using Sven.GraphManagement;
using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Query;

namespace Sc4ve.Multimodality.Intent
{
    public class PointParameter : Parameter
    {
        [SerializeField] private string _value;
        [JsonProperty("value")]
        public string Value
        {
            get => _value;
            set => _value = value;
        }

        [SerializeField] private DateTime _timestamp;
        [JsonProperty("timestamp")]
        public DateTime Timestamp
        {
            get => _timestamp;
            set => _timestamp = value;
        }

        [SerializeField] private Vector3? _point;
        [JsonIgnore]
        public Vector3? Point
        {
            get => _point;
            set => _point = value;
        }

        public async Task<Vector3?> QueryPoint()
        {
            string intervalQuery = @$"{{
        SELECT DISTINCT ?interval
        WHERE {{
            VALUES ?instantTime {{ ""{Timestamp:yyyy-MM-ddTHH:mm:ss.fffzzz}""^^xsd:dateTime }}
            ?interval a time:Interval ;
                    time:hasBeginning/time:inXSDDateTime ?startTime .
            OPTIONAL {{
                ?interval time:hasEnd/time:inXSDDateTime ?_endTime .
            }}
            BIND(IF(BOUND(?_endTime), ?_endTime, NOW()) AS ?endTime)
            FILTER(?startTime <= ?instantTime && ?instantTime < ?endTime)
        }} ORDER BY ?startTime ?endTime limit 10000
    }}";
            // execute sparql query to get color from value
            string query = $@"PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX sc4ve: <https://sc4ve.lisn.upsaclay.fr/ontology#>
PREFIX time: <http://www.w3.org/2006/time#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>

SELECT DISTINCT ?x ?y ?z
WHERE {{
    {intervalQuery}
    ?object sven:component ?pointer .
    ?pointer a sven:Pointer ;
             sven:pointerHitPosition ?pointerHitPosition .
    ?pointerHitPosition sven:hasTemporalExtent ?interval ;
                        sven:x ?x ;
                        sven:y ?y ;
                        sven:z ?z .
}} LIMIT 1";
            // execute query and parse result
            // extract color components from first result
            if (GraphManager.Instance.ExecuteQuery(query) is SparqlResultSet results && results.Count > 0)
            {
                SparqlResult result = (SparqlResult)results.Results[0];

                // Use ILiteralNode.Value and invariant culture parsing to avoid formatting issues

                if (result["x"] is not ILiteralNode xNode ||
                    result["y"] is not ILiteralNode yNode ||
                    result["z"] is not ILiteralNode zNode)
                {
                    Debug.LogWarning("QueryPoint: missing x, y, or z component in query result.");
                    return null;
                }

                // Parse strictly with invariant culture so decimal separator is '.'
                bool okX = float.TryParse(xNode.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float xVal);
                bool okY = float.TryParse(yNode.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float yVal);
                bool okZ = float.TryParse(zNode.Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float zVal);

                if (!okX || !okY || !okZ)
                {
                    Debug.LogWarning("QueryPoint: failed to parse x, y, or z component from query result.");
                    return null;
                }

                return new Vector3(xVal, yVal, zVal);
            }
            else
            {
                return null;
            }
        }

        public override async Task<IUriNode> Semanticize(Graph graph)
        {
            IUriNode parameterNode = await base.Semanticize(graph);

            Point ??= await QueryPoint();
            if (Point != null)
            {
                IUriNode x = graph.CreateUriNode("sven:x");
                IUriNode y = graph.CreateUriNode("sven:y");
                IUriNode z = graph.CreateUriNode("sven:z");
                // insert triples for color components (0 to 1) — use InvariantCulture so ToString uses '.'
                graph.Assert(new Triple(parameterNode, x, graph.CreateLiteralNode(((Vector3)Point).x.ToString(CultureInfo.InvariantCulture), graph.CreateUriNode("xsd:float").Uri)));
                graph.Assert(new Triple(parameterNode, y, graph.CreateLiteralNode(((Vector3)Point).y.ToString(CultureInfo.InvariantCulture), graph.CreateUriNode("xsd:float").Uri)));
                graph.Assert(new Triple(parameterNode, z, graph.CreateLiteralNode(((Vector3)Point).z.ToString(CultureInfo.InvariantCulture), graph.CreateUriNode("xsd:float").Uri)));
            }
            return parameterNode;
        }
    }
}