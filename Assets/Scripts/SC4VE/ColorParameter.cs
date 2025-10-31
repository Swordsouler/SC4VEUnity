using Newtonsoft.Json;
using UnityEngine;

namespace Sc4ve.Multimodality
{
    public class ColorParameter : Parameter
    {
        [SerializeField] private string _value;
        [JsonProperty("value")]
        public string Value
        {
            get => _value;
            set => _value = value;
        }
    }
}