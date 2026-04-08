using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sven.Command
{
    [Serializable]
    public class AnnotationParameter : IBaseParameter
    {
        private string _annotationType = string.Empty;
        public string AnnotationType
        {
            get => _annotationType;
            set => _annotationType = value;
        }

        public List<GameObject> Prefabs { get; set; } = new();

        public AnnotationParameter() { }

        public AnnotationParameter(AnnotationFilterEntry entry)
        {
            if (entry == null) return;
            AnnotationType = entry.AnnotationParameter.AnnotationType;
            Prefabs = new List<GameObject>(entry.AnnotationParameter.Prefabs);
        }
    }
}