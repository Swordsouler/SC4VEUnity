using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
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

        [SerializeField] private string _limit;
        [JsonProperty("limit")]
        public string Limit
        {
            get => _limit;
            set => _limit = value;
        }

        [SerializeField] private Order _order;
        [JsonProperty("order")]
        public Order Order
        {
            get => _order;
            set => _order = value;
        }

        public async Task<List<string>> QueryObjects(Graph queryGraph)
        {
            string locale = MultimodalityController.LoadedLocale;
            // execute sparql query to get color from value
            string query = "";/*$@"
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
}}";*/
            SparqlResultSet results = null;
            List<string> objectsUri = new();
            foreach (SparqlResult result in results.Cast<SparqlResult>())
            {
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

            List<string> objectsUri = await QueryObjects(graph);
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

        [SerializeField] private bool _desc;
        [JsonProperty("desc")]
        public bool Desc
        {
            get => _desc;
            set => _desc = value;
        }
    }

    public class Condition
    {
        [SerializeField] private string _operator;
        [JsonProperty("operator")]
        public string Operator
        {
            get => _operator;
            set => _operator = value;
        }

        [SerializeField] private string _type;
        [JsonProperty("type")]
        public string Type
        {
            get => _type;
            set => _type = value;
        }

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