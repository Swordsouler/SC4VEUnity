using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Sc4ve.Multimodality.Parameter
{
    public class PositionParameter : Parameter
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
    }
}