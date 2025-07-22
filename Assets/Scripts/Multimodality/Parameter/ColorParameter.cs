using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Sven.Command
{
    [Serializable]
    public class ColorParameter : IBaseParameter
    {
        private float _red = 0.5f;
        public float Red
        {
            get => _red;
            set => _red = Mathf.Clamp01(value);
        }

        private float _green = 0.5f;
        public float Green
        {
            get => _green;
            set => _green = Mathf.Clamp01(value);
        }

        private float _blue = 0.5f;
        public float Blue
        {
            get => _blue;
            set => _blue = Mathf.Clamp01(value);
        }

        private float _tolerance = 0.1f;
        public float Tolerance
        {
            get => _tolerance;
            set => _tolerance = Mathf.Clamp(value, 0f, 1f);
        }

        public bool IsMatching(Color color)
        {
            return Mathf.Abs(color.r - Red) <= Tolerance &&
                   Mathf.Abs(color.g - Green) <= Tolerance &&
                   Mathf.Abs(color.b - Blue) <= Tolerance;
        }

        // ignore serialization for these properties
        [JsonIgnore]
        public Color MaxColor =>
            new(
                Mathf.Clamp01(Red + Tolerance),
                Mathf.Clamp01(Green + Tolerance),
                Mathf.Clamp01(Blue + Tolerance)
            );
        [JsonIgnore]
        public Color MinColor =>
            new(
                Mathf.Clamp01(Red - Tolerance),
                Mathf.Clamp01(Green - Tolerance),
                Mathf.Clamp01(Blue - Tolerance)
            );
    }
}