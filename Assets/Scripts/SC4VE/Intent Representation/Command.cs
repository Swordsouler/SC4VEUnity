using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality
{
    [Serializable]
    public class Command
    {
        [SerializeField] private string _type;
        [JsonProperty("type")]
        public string Type
        {
            get => _type;
            set => _type = value;
        }

        [SerializeField] private List<Parameter> _parameters;
        [JsonProperty("parameters")]
        public List<Parameter> Parameters
        {
            get => _parameters;
            set => _parameters = value;
        }
    }
}