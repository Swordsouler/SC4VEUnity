using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality
{
    public class SelectionParameter : Parameter
    {
        [SerializeField] private List<FilterElement> _filter;
        [JsonProperty("filter")]
        [JsonConverter(typeof(FilterElementListConverter))]
        public List<FilterElement> Filter
        {
            get => _filter;
            set => _filter = value;
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
    }

    public class Order
    {
        [SerializeField] private List<Criteria> _criterias;
        [JsonProperty("criteria")]
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