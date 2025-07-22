using Sven.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sven.Command
{
    [Serializable]
    public class AnnotationFilterSettings : BaseCommandSettings
    {
        public List<AnnotationFilterEntry> Entries = new();
        [NonSerialized] private Vector2 _scroll;

        public override void OnGUI(MultimodalitySettingsWindow window)
        {
            var availableAnnotationTypes = ISemanticAnnotation.GetAvailableAnnotationTypes();
            SynchronizeEntries(availableAnnotationTypes, window);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Annotation filter entries", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            foreach (var entry in Entries)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField(entry.AnnotationType, EditorStyles.boldLabel);
                entry.DrawTriggerWordsUI(window);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndScrollView();
        }

        private void SynchronizeEntries(string[] availableTypes, MultimodalitySettingsWindow window)
        {
            bool changed = false;
            var currentTypesInEntries = Entries.Select(e => e.AnnotationType).ToList();

            foreach (var type in availableTypes)
            {
                if (!currentTypesInEntries.Contains(type))
                {
                    Entries.Add(new AnnotationFilterEntry { AnnotationType = type });
                    changed = true;
                }
            }

            if (Entries.RemoveAll(e => !availableTypes.Contains(e.AnnotationType)) > 0)
            {
                changed = true;
            }

            if (changed)
            {
                Entries = Entries.OrderBy(e => e.AnnotationType).ToList();
                window.SaveSettings();
            }
        }
    }

    [Serializable]
    public class AnnotationFilterEntry
    {
        public string AnnotationType { get; set; } = string.Empty;
        public List<string> TriggerWords { get; set; } = new();

        [NonSerialized] private TriggerWordsDrawer _triggerWordsDrawer;

        public void DrawTriggerWordsUI(MultimodalitySettingsWindow window)
        {
            _triggerWordsDrawer ??= new TriggerWordsDrawer("Trigger Words");
            _triggerWordsDrawer.Draw(window, TriggerWords);
        }
    }
}