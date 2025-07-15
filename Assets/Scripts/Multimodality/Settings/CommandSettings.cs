using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sven.Command
{
    [Serializable]
    public class CommandSettings : BaseCommandSettings
    {
        private List<string> _triggerWords = new();
        public List<string> TriggerWords
        {
            get => _triggerWords ??= new List<string>();
            set => _triggerWords = value ?? new List<string>();
        }

        public CommandSettings()
        {
            _triggerWords = new List<string>();
        }

        [NonSerialized] private string _newTriggerWord = "";
        [NonSerialized] private bool _requestFocus = false;
        [NonSerialized] private string _duplicateError = "";
        [NonSerialized] private Vector2 _scroll;
        [NonSerialized] private bool _wasTextFieldFocused = false;
        [NonSerialized] private bool _forceFocus = false;

        public override void OnGUI(MultimodalitySettingsWindow window)
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Trigger words", EditorStyles.boldLabel);

            bool addRequested = HandleInputField();

            if (addRequested)
            {
                _forceFocus = true;
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

            EditorGUILayout.Space();

            DrawTriggerWords(window);

            EditorGUILayout.Space();
            EditorGUILayout.EndScrollView();
        }

        private bool HandleInputField()
        {
            Event e = Event.current;
            bool addRequested = false;

            EditorGUILayout.BeginHorizontal();
            GUI.SetNextControlName("NewTriggerWordField");
            _newTriggerWord = EditorGUILayout.TextField(_newTriggerWord);

            if (GUILayout.Button("Add", GUILayout.Width(60)))
            {
                addRequested = true;
                _forceFocus = true;
            }
            EditorGUILayout.EndHorizontal();

            // Ajout par touche Entrée (KeyDown) si le champ a le focus
            if (e.type == EventType.KeyDown &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "NewTriggerWordField")
            {
                addRequested = true;
                _forceFocus = true;
                e.Use();
            }

            if (_forceFocus || _requestFocus)
            {
                EditorApplication.delayCall += () =>
                {
                    EditorGUI.FocusTextInControl("NewTriggerWordField");
                };
                _forceFocus = false;
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
                    TriggerWords.Add(trimmed);
                    _duplicateError = "";
                    window.SaveSettings();
                }
            }
            _newTriggerWord = "";
            _requestFocus = true;
        }

        private void DrawTriggerWords(MultimodalitySettingsWindow window)
        {
            float viewWidth = EditorGUIUtility.currentViewWidth - 40;
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