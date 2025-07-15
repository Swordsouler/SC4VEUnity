using System;
using System.Collections.Generic;
using UnityEditor;

namespace Sven.Command
{
    [Serializable]
    public class ColorFilterSettings : BaseCommandSettings
    {
        public override void OnGUI(Action onChanged)
        {
            EditorGUILayout.LabelField("Color filter parameters here!");
        }
    }

    [Serializable]
    public class ColorFilterEntry
    {
        private float _redThreshold;
        public float RedThreshold
        {
            get => _redThreshold;
            set => _redThreshold = value < 0f ? 0f : (value > 1f ? 1f : value);
        }
        private float _greenThreshold;
        public float GreenThreshold
        {
            get => _greenThreshold;
            set => _greenThreshold = value < 0f ? 0f : (value > 1f ? 1f : value);
        }
        private float _blueThreshold;
        public float BlueThreshold
        {
            get => _blueThreshold;
            set => _blueThreshold = value < 0f ? 0f : (value > 1f ? 1f : value);
        }

        private List<string> _triggerWords = new();
        public List<string> TriggerWords
        {
            get => _triggerWords ??= new List<string>();
            set => _triggerWords = value ?? new List<string>();
        }

        public ColorFilterEntry()
        {
            _triggerWords = new List<string>();
        }
    }
}