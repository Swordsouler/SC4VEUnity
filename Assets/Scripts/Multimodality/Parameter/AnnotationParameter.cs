using System;

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

        public AnnotationParameter() { }

        public AnnotationParameter(AnnotationFilterEntry entry)
        {
            if (entry == null) return;
            AnnotationType = entry.AnnotationParameter.AnnotationType;
        }
    }
}