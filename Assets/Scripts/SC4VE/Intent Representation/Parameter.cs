using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Sc4ve.Multimodality
{
    [JsonConverter(typeof(ParameterConverter))]
    [Serializable]
    public class Parameter
    {
        [SerializeField] private string _type;
        [JsonProperty("type")]
        public string Type
        {
            get => _type;
            set => _type = value;
        }
    }
}