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

            // Ajouter les nouvelles entrées
            foreach (var type in availableTypes)
            {
                if (!currentTypesInEntries.Contains(type))
                {
                    Entries.Add(new AnnotationFilterEntry { AnnotationType = type });
                    changed = true;
                }
            }

            // Supprimer les anciennes entrées
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
            _controlName = Guid.NewGuid().ToString();
        }

        [NonSerialized] private string _newTriggerWord = "";
        [NonSerialized] private bool _requestFocus = false;
        [NonSerialized] private string _duplicateError = "";
        [NonSerialized] private string _controlName;

        public void DrawTriggerWordsUI(MultimodalitySettingsWindow window)
        {
            bool addRequested = HandleInputField();

            if (addRequested)
            {
                TryAddTriggerWord(window);
            }

            if (!string.IsNullOrEmpty(_duplicateError))
            {
                GUIStyle errorStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = Color.red }
                };
                EditorGUILayout.LabelField(_duplicateError, errorStyle);
            }

            DrawTriggerWords(window);
        }

        private bool HandleInputField()
        {
            Event e = Event.current;
            bool addRequested = false;
            EditorGUILayout.BeginHorizontal();
            GUI.SetNextControlName(_controlName);
            _newTriggerWord = EditorGUILayout.TextField(_newTriggerWord);

            if (GUILayout.Button("Add", GUILayout.Width(60)))
            {
                addRequested = true;
            }
            EditorGUILayout.EndHorizontal();

            if (e.type == EventType.KeyUp &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == _controlName)
            {
                addRequested = true;
                e.Use();
            }

            if (_requestFocus)
            {
                EditorApplication.delayCall += () =>
                {
                    EditorGUI.FocusTextInControl(_controlName);
                };
                _requestFocus = false;
            }

            return addRequested;
        }

        private void TryAddTriggerWord(MultimodalitySettingsWindow window)
        {
            string trimmed = _newTriggerWord.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                if (TriggerWords.Contains(trimmed))
                {
                    _duplicateError = $"Word \"{_newTriggerWord}\" is already used in this block.";
                }
                else if (SettingsWordUtils.IsWordUsed(trimmed, window, out var foundInType))
                {
                    _duplicateError = $"Word \"{_newTriggerWord}\" is already used in {foundInType}.";
                }
                else
                {
                    TriggerWords.Add(trimmed.ToLower());
                    _duplicateError = "";
                    window.SaveSettings();
                }
            }
            _newTriggerWord = "";
            _requestFocus = true;
        }

        private void DrawTriggerWords(MultimodalitySettingsWindow window)
        {
            float viewWidth = EditorGUIUtility.currentViewWidth - 60;
            List<List<int>> lines = new();
            List<int> currentLine = new();
            float currentLineWidth = 0;

            for (int i = 0; i < TriggerWords.Count; i++)
            {
                string tagText = TriggerWords[i];
                GUIStyle tagStyle = GetTagStyle();
                Vector2 textSize = tagStyle.CalcSize(new GUIContent(tagText));
                float tagWidth = textSize.x + 18 + 6;

                if (currentLine.Count > 0 && currentLineWidth + tagWidth > viewWidth)
                {
                    lines.Add(currentLine);
                    currentLine = new();
                    currentLineWidth = 0;
                }
                currentLine.Add(i);
                currentLineWidth += tagWidth;
            }
            if (currentLine.Count > 0)
                lines.Add(currentLine);

            int removeIndex = -1;
            foreach (var line in lines)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(4);
                foreach (var i in line)
                {
                    string tagText = TriggerWords[i];
                    GUIStyle tagStyle = GetTagStyle();
                    Color prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.85f, 0.92f, 1f, 1f);

                    Vector2 textSize = tagStyle.CalcSize(new GUIContent(tagText));
                    float tagWidth = textSize.x + 18;

                    EditorGUILayout.BeginHorizontal(tagStyle, GUILayout.Width(tagWidth));
                    EditorGUILayout.LabelField(tagText, GUILayout.MinWidth(tagWidth - 18 - 6), GUILayout.ExpandWidth(false));
                    GUILayout.Space(2);
                    if (GUILayout.Button("x", GUILayout.Width(18), GUILayout.Height(18)))
                        removeIndex = i;
                    EditorGUILayout.EndHorizontal();

                    GUI.backgroundColor = prevBg;
                    GUILayout.Space(4);
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4);
            }

            if (removeIndex >= 0)
            {
                TriggerWords.RemoveAt(removeIndex);
                window.SaveSettings();
            }
        }

        private GUIStyle GetTagStyle()
        {
            return new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(4, 4, 4, 4),
                margin = new RectOffset(0, 0, 0, 0),
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 0
            };
        }
    }
}