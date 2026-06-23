using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sven.Content;
using Sven.GraphManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Query;

namespace Sc4ve.Multimodality.Intent
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

        [SerializeField] private List<string> _objectsUri;
        [JsonIgnore]
        public List<string> ObjectsUri
        {
            get => _objectsUri;
            set => _objectsUri = value;
        }
        public List<string> ObjectsId => ObjectsUri?.Select(uri => uri.Split('/').Last()).ToList();
        [JsonIgnore]
        public List<SemantizationCore> Objects => ObjectsId?.Select(id =>
        {
            SemantizationExtensions.TryGetComponentByUUID(id, out Component obj);
            if (obj is SemantizationCore sc)
                return sc;
            return null;
        }).Where(obj => obj != null).ToList();

        [JsonIgnore] public bool HasCoreferenceCondition => Filters != null && Filters.Any(f => !f.IsOperator && f.Condition.IsCoreference);
        [JsonIgnore] public string OrderSparqlTail => Order != null ? Order.SparqlTail : string.Empty;
        [JsonIgnore] public string OrderSparqlBody => Order != null ? Order.SparqlBody : string.Empty;

        public Task<List<string>> QueryObjects(Graph queryGraph)
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
            Debug.Log(query + " " + JsonConvert.SerializeObject(this));
            SparqlResultSet results = queryGraph.ExecuteQuery(query) as SparqlResultSet;
            List<string> objectsUri = new();
            foreach (SparqlResult result in results.Cast<SparqlResult>())
            {
                if (result.HasBoundValue("object") && result["object"] != null)
                {
                    string objUri = result["object"].ToString();
                    objectsUri.Add(objUri);
                }
            }
            if (objectsUri.Count == 0)
                Debug.LogWarning($"[SelectionParameter] Requête retourne 0 objet. Vérifier les timestamps des filtres.");
            else
                Debug.Log($"[SelectionParameter] {objectsUri.Count} objet(s) trouvé(s).");
            return Task.FromResult(objectsUri);
        }

        public override async Task<IUriNode> Semanticize(Graph graph)
        {
            IUriNode parameterNode = await base.Semanticize(graph);

            Graph sceneGraphCopy = GraphManager.InstanceCopy();

            // apply ontology inference (for annotation)
            await GraphManager.ApplyOntologyAsync(sceneGraphCopy);

            // execute this query :
            /*string queryTest = $@"
PREFIX sc4ve: <https://sc4ve.lisn.upsaclay.fr/ontology#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX time: <http://www.w3.org/2006/time#>
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
SELECT *
WHERE
{{
    {{
        ?object sven:component ?component .
        ?component sven:color ?property .
        ?property a sven:Color ;
        sven:hasTemporalExtent ?interval2 ;
        sven:r ?r2 ;
        sven:g ?g2 ;
        sven:b ?b2 ;
        sven:a ?a2 .
    }}
    {{
        ?color a sven:Color ;
        rdfs:label ""Rouge""@fr ;
        sven:r ?tr2 ;
        sven:g ?tg2 ;
        sven:b ?tb2 ;
        sc4ve:tolerance ?t2 .
        BIND(?tr2 - ?t2 AS ?minR2)
        BIND(?tr2 + ?t2 AS ?maxR2)
        BIND(?tg2 - ?t2 AS ?minG2)
        BIND(?tg2 + ?t2 AS ?maxG2)
        BIND(?tb2 - ?t2 AS ?minB2)
        BIND(?tb2 + ?t2 AS ?maxB2)
    }}
    FILTER(?minR2 <= ?r2 && ?r2 <= ?maxR2 && ?minG2 <= ?g2 && ?g2 <= ?maxG2 && ?minB2 <= ?b2 && ?b2 <= ?maxB2)
}} LIMIT 10000";
            SparqlResultSet results = sceneGraphCopy.ExecuteQuery(queryTest) as SparqlResultSet;
            Debug.Log("Test query results:");
            foreach (SparqlResult result in results.Cast<SparqlResult>())
            {
                // objectName
                Debug.Log(result);
            }*/

            /******************************************/
            List<string> objectsUri = await QueryObjects(sceneGraphCopy);
            // if contains IsCoreference condition, also add coreferenced objects
            if (HasCoreferenceCondition)
                foreach (string objUri in Command.LastObjectIds)
                    if (!objectsUri.Contains(objUri))
                        objectsUri.Add(objUri);
            ObjectsUri ??= objectsUri;

            foreach (string objectUri in ObjectsUri)
            {
                IUriNode hasObject = graph.CreateUriNode("sven:value");
                IUriNode objectNode = graph.CreateUriNode(UriFactory.Create(objectUri));
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
        [JsonIgnore] public bool IsCoreference => Type.ToLower() == "coreference";

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
            string locale = UserData.Locale;
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
            ?component a sven:Annotator ;
                       sven:annotation ?annotation .
            ?annotation sven:value ?componentType ;
                        sven:hasTemporalExtent ?interval{index} .
            ?componentType rdfs:label ""{EscapeSparqlLiteral(Value)}""@{locale}
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
                        rdfs:label ""{EscapeSparqlLiteral(Value)}""@fr ;
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
            }}
            FILTER(?minR{index} <= ?r{index} && ?r{index} <= ?maxR{index} && ?minG{index} <= ?g{index} && ?g{index} <= ?maxG{index} && ?minB{index} <= ?b{index} && ?b{index} <= ?maxB{index})
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
            ?componentType rdfs:label ""{EscapeSparqlLiteral(Value)}""@{locale}
        }} LIMIT 10000
    }}";
            }
            else if (IsCoreference)
            {
                // tolérance : also take objects in Command.LastObjectIds even if not in interval
                return $@"";
            }

            Debug.LogWarning($"Condition.Sparql: Unknown condition type '{Type}'");
            return "";
        }

        /// <summary>
        /// Échappe une valeur pour un littéral de chaîne SPARQL (antislash et guillemets).
        /// Les valeurs viennent d'un vocabulaire contrôlé, mais on échappe par robustesse.
        /// </summary>
        private static string EscapeSparqlLiteral(string value)
            => value?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? string.Empty;
    }

    public class FilterElement
    {
        [SerializeField] private bool _isOperator = false;
        [JsonProperty("isOperator")]
        public bool IsOperator
        {
            get => _isOperator;
            set => _isOperator = value;
        }

        [JsonIgnore] public bool IsOr => IsOperator && Operator.ToLower() == "or";
        [JsonIgnore] public bool IsAnd => IsOperator && Operator.ToLower() == "and";

        [SerializeField] private string _operator = string.Empty;
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