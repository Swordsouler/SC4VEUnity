using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sven.GraphManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Query;

namespace Sc4ve.Multimodality.Parameter
{
    public class SelectionParameter : Parameter
    {
        [SerializeField] private List<FilterElement> _filters;
        [JsonProperty("filters")]
        [JsonConverter(typeof(FilterElementListConverter))]
        public List<FilterElement> Filters
        {
            get => _filters;
            set => _filters = value;
        }

        [JsonIgnore]
        public string FiltersSparql
        {
            get
            {
                if (Filters == null || Filters.Count == 0)
                    return string.Empty;

                List<string> parts = Filters.Select((f, i) => f.Sparql(i)).ToList();
                StringBuilder sb = new();

                foreach (string p in parts)
                {
                    if (!string.IsNullOrEmpty(p))
                        sb.AppendLine(p.TrimEnd());
                }

                // add "}" at the end for each IsAnd
                int opens = Filters.Count(f => f.IsAnd);
                // \n must be a join
                for (int i = 0; i < opens; i++)
                    sb.AppendLine("    }");

                return sb.ToString();
            }
        }

        [SerializeField] private int _limit;
        [JsonProperty("limit")]
        public int Limit
        {
            get => _limit;
            set => _limit = value;
        }

        [JsonIgnore] public string LimitSparql => Limit > 0 ? $"LIMIT {Limit}" : "LIMIT 10000";

        [SerializeField] private Order _order;
        [JsonProperty("order")]
        public Order Order
        {
            get => _order;
            set => _order = value;
        }

        [JsonIgnore] public string OrderSparqlTail => Order != null ? Order.SparqlTail : string.Empty;
        [JsonIgnore] public string OrderSparqlBody => Order != null ? Order.SparqlBody : string.Empty;

        public async Task<List<string>> QueryObjects()
        {
            // execute sparql query to get color from value
            string query = $@"PREFIX sc4ve: <https://sc4ve.lisn.upsaclay.fr/ontology#>
PREFIX time: <http://www.w3.org/2006/time#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>

SELECT DISTINCT ?object
WHERE {{
{FiltersSparql}
{OrderSparqlBody}
}} {OrderSparqlTail} {LimitSparql}";
            Debug.Log(query);
            SparqlResultSet results = GraphManager.Instance.ExecuteQuery(query) as SparqlResultSet;
            List<string> objectsUri = new();
            foreach (SparqlResult result in results.Cast<SparqlResult>())
            {
                Debug.Log(result.ToString());
                if (result["object"] != null)
                {
                    string objUri = result["object"].ToString();
                    objectsUri.Add(objUri);
                }
            }
            return objectsUri;
        }

        public override async Task<IUriNode> Semanticize(Graph graph)
        {
            IUriNode parameterNode = await base.Semanticize(graph);

            List<string> objectsUri = await QueryObjects();
            foreach (string objectUri in objectsUri)
            {
                IUriNode hasObject = graph.CreateUriNode("sven:value");
                IUriNode objectNode = graph.CreateUriNode($"<{objectUri}>");
                graph.Assert(new Triple(parameterNode, hasObject, objectNode));
            }
            return parameterNode;
        }
    }

    public class Order
    {
        [SerializeField] private List<Criteria> _criterias;
        [JsonProperty("criterias")]
        public List<Criteria> Criterias
        {
            get => _criterias;
            set => _criterias = value;
        }

        [JsonIgnore]
        public string SparqlBody
        {
            get
            {
                if (Criterias != null && Criterias.Count > 0)
                {
                    return string.Join("\n", Criterias.Select(c => c.SparqlBody));
                }
                return string.Empty;
            }
        }

        [JsonIgnore]
        public string SparqlTail
        {
            get
            {
                if (Criterias != null && Criterias.Count > 0)
                {
                    return "ORDER BY " + string.Join(" ", Criterias.Select(c => $"{c.SparqlTail}(?{c.Type})"));
                }
                return string.Empty;
            }
        }
    }

    public class Criteria
    {
        [SerializeField] private string _type;
        [JsonProperty("type")]
        public string Type
        {
            get => _type;
            set => _type = value;
        }
        [JsonIgnore] public bool IsName => Type.ToLower() == "name";
        [JsonIgnore] public bool IsSize => Type.ToLower() == "size";

        [SerializeField] private bool _desc;
        [JsonProperty("desc")]
        public bool Desc
        {
            get => _desc;
            set => _desc = value;
        }

        [JsonIgnore]
        public string SparqlTail => Desc ? "DESC" : "ASC";

        [JsonIgnore]
        public string SparqlBody
        {
            get
            {
                return Type switch
                {
                    "name" => @"    ?object rdfs:label ?name .",
                    "size" => @"    ?object sven:component/sven:scale ?scale .
    ?scale sven:x ?x ;
           sven:y ?y ;
           sven:z ?z .
    BIND(?x + ?y + ?z AS ?size)",
                    _ => string.Empty,
                };
            }
        }
    }

    public class Condition
    {
        [SerializeField] private string _type;
        [JsonProperty("type")]
        public string Type
        {
            get => _type;
            set => _type = value;
        }
        [JsonIgnore] public bool IsAnnotation => Type.ToLower() == "annotation";
        [JsonIgnore] public bool IsColor => Type.ToLower() == "color";
        [JsonIgnore] public bool IsEvent => Type.ToLower() == "event";

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

        public string Sparql(int index)
        {
            string locale = MultimodalityController.LoadedLocale;
            string intervalQuery = @$"{{
                SELECT DISTINCT ?interval{index}
                WHERE {{
                    VALUES ?instantTime {{ ""{Timestamp:yyyy-MM-ddTHH:mm:ss.fffzzz}""^^xsd:dateTime }}
                    ?interval{index} a time:Interval ;
                            time:hasBeginning/time:inXSDDateTime ?startTime .
                    OPTIONAL {{
                        ?interval{index} time:hasEnd/time:inXSDDateTime ?_endTime .
                    }}
                    BIND(IF(BOUND(?_endTime), ?_endTime, NOW()) AS ?endTime)
                    FILTER(?startTime <= ?instantTime && ?instantTime < ?endTime)
                }} ORDER BY ?startTime ?endTime limit 10000
            }}";
            if (IsAnnotation)
            {
                return @$"{{
        SELECT DISTINCT ?object
        WHERE
        {{
            {intervalQuery}
            ?object sven:component ?component .
            ?component a ?componentType ;
                        sven:hasTemporalExtent ?interval{index} .
            ?componentType rdfs:label ""{Value}""@{locale}
        }} LIMIT 10000
    }}";
            }
            else if (IsColor)
            {
                return @$"{{
        SELECT DISTINCT ?object
        WHERE
        {{
            {{
                {intervalQuery}
                ?object sven:component ?component .
                ?component sven:color ?property .
                ?property a sven:Color ;
                            sven:hasTemporalExtent ?interval{index} ;
                            sven:r ?r{index} ;
                            sven:g ?g{index} ;
                            sven:b ?b{index} ;
                            sven:a ?a{index} .
            }}
            {{
                ?color a sven:Color ;
                        rdfs:label ""Rouge""@fr ;
                        sven:r ?tr{index} ;
                        sven:g ?tg{index} ;
                        sven:b ?tb{index} ;
                        sc4ve:tolerance ?t{index} .
                BIND(?tr{index} - ?t{index} AS ?minR{index})
                BIND(?tr{index} + ?t{index} AS ?maxR{index})
                BIND(?tg{index} - ?t{index} AS ?minG{index})
                BIND(?tg{index} + ?t{index} AS ?maxG{index})
                BIND(?tb{index} - ?t{index} AS ?minB{index})
                BIND(?tb{index} + ?t{index} AS ?maxB{index})
                FILTER(?minR{index} <= ?r{index} && ?r{index} <= ?maxR{index} && ?minG{index} <= ?g{index} && ?g{index} <= ?maxG{index} && ?minB{index} <= ?b{index} && ?b{index} <= ?maxB{index})
            }}
        }} LIMIT 10000
    }}";
            }
            else if (IsEvent)
            {
                return @$"{{
        SELECT DISTINCT ?object
        WHERE
        {{
            {intervalQuery}
            ?event a sven:CollisionEvent ;
            	    sven:hasTemporalExtent ?interval{index} ;
            	    sven:sender ?sender ;
                    sven:receiver ?object .
            ?sender sven:component ?component .
            ?component a ?componentType .
            ?componentType rdfs:label ""{Value}""@{locale}
        }} LIMIT 10000
    }}";
            }
            Debug.LogWarning($"Condition.Sparql: Unknown condition type '{Type}'");
            return "";
        }
    }

    public class FilterElement
    {
        [SerializeField] private bool _isOperator;
        [JsonProperty("isOperator")]
        public bool IsOperator
        {
            get => _isOperator;
            set => _isOperator = value;
        }

        [JsonIgnore] public bool IsOr => IsOperator && Operator.ToLower() == "or";
        [JsonIgnore] public bool IsAnd => IsOperator && Operator.ToLower() == "and";

        [SerializeField] private string _operator;
        [JsonProperty("operator")]
        public string Operator
        {
            get => _operator;
            set => _operator = value;
        }

        [SerializeField] private Condition _condition;
        [JsonProperty("condition")]
        public Condition Condition
        {
            get => _condition;
            set => _condition = value;
        }

        public string Sparql(int index)
        {
            if (IsAnd)
            {
                return "    {";
            }
            else if (IsOr)
            {
                return "    UNION";
            }
            else
            {
                return $"    {Condition.Sparql(index)}";
            }
        }
    }

    internal class FilterElementListConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(List<FilterElement>).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var arr = JArray.Load(reader);
            var list = new List<FilterElement>(arr.Count);

            foreach (var token in arr)
            {
                if (token.Type == JTokenType.String)
                {
                    list.Add(new FilterElement { IsOperator = true, Operator = token.ToObject<string>() });
                }
                else if (token.Type == JTokenType.Object)
                {
                    var cond = token.ToObject<Condition>(serializer);
                    list.Add(new FilterElement { IsOperator = false, Condition = cond });
                }
                else
                {
                    // tolérance : convertit en chaîne
                    list.Add(new FilterElement { IsOperator = true, Operator = token.ToString() });
                }
            }

            return list;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var list = value as List<FilterElement>;
            writer.WriteStartArray();
            if (list != null)
            {
                foreach (var el in list)
                {
                    if (el.IsOperator)
                        writer.WriteValue(el.Operator);
                    else
                        serializer.Serialize(writer, el.Condition);
                }
            }
            writer.WriteEndArray();
        }
    }
}