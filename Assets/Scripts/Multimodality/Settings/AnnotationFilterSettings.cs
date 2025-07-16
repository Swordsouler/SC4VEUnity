using System;
using System.Collections.Generic;
using UnityEditor;

namespace Sven.Command
{
    // pour chaque IComponentMapping, on doit pouvoir définir une liste de mots déclencheurs

    [Serializable]
    public class AnnotationFilterSettings : BaseCommandSettings
    {
        public override void OnGUI(MultimodalitySettingsWindow window)
        {
            EditorGUILayout.LabelField("Not yet developped");
        }
    }

    [Serializable]
    public class AnnotationFilterEntry
    {
        private string _annotationType;
        public string AnnotationType
        {
            get => _annotationType;
            set => _annotationType = value ?? string.Empty;
        }

        private List<string> _triggerWords = new();
        public List<string> TriggerWords
        {
            get => _triggerWords ??= new List<string>();
            set => _triggerWords = value ?? new List<string>();
        }

        public AnnotationFilterEntry()
        {
            _triggerWords = new List<string>();
        }
    }
}