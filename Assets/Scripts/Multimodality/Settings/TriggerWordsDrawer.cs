using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sven.Command
{
    public class TriggerWordsDrawer
    {
        private string _newWord = "";
        private bool _requestFocus = false;
        private string _duplicateError = "";
        private readonly string _controlName = Guid.NewGuid().ToString();
        private readonly string _listName;

        public TriggerWordsDrawer(string listName)
        {
            _listName = listName;
        }

        public void Draw(S4MSettingsWindow window, List<string> words)
        {
            EditorGUILayout.LabelField(_listName, EditorStyles.boldLabel);

            if (HandleInputField(words, window))
            {
                TryAddWord(words, window);
            }

            if (!string.IsNullOrEmpty(_duplicateError))
            {
                GUIStyle errorStyle = new(EditorStyles.label)
                {
                    normal = { textColor = Color.red }
                };
                EditorGUILayout.LabelField(_duplicateError, errorStyle);
            }

            EditorGUILayout.Space();
            DrawWords(words, window);
        }

        private bool HandleInputField(List<string> words, S4MSettingsWindow window)
        {
            Event e = Event.current;
            bool addRequested = false;

            EditorGUILayout.BeginHorizontal();
            GUI.SetNextControlName(_controlName);
            _newWord = EditorGUILayout.TextField(_newWord);

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
                EditorApplication.delayCall += () => EditorGUI.FocusTextInControl(_controlName);
                _requestFocus = false;
            }

            return addRequested;
        }

        private void TryAddWord(List<string> words, S4MSettingsWindow window)
        {
            string trimmed = _newWord.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                if (words.Contains(trimmed))
                {
                    _duplicateError = $"Word \"{_newWord}\" is already used in this list.";
                }
                else if (IsWordUsed(trimmed, window, out var foundInType, words))
                {
                    _duplicateError = $"Word \"{_newWord}\" is already used in {foundInType}.";
                }
                else
                {
                    words.Add(trimmed.ToLower());
                    _duplicateError = "";
                    window.SaveSettings();
                }
            }
            _newWord = "";
            _requestFocus = true;
        }

        private void DrawWords(List<string> words, S4MSettingsWindow window)
        {
            float viewWidth = EditorGUIUtility.currentViewWidth - 40;
            var lines = new List<List<int>>();
            var currentLine = new List<int>();
            float currentLineWidth = 0;

            for (int i = 0; i < words.Count; i++)
            {
                GUIStyle tagStyle = GetTagStyle();
                Vector2 textSize = tagStyle.CalcSize(new GUIContent(words[i]));
                float tagWidth = textSize.x + 18 + 6;

                if (currentLine.Count > 0 && currentLineWidth + tagWidth > viewWidth)
                {
                    lines.Add(currentLine);
                    currentLine = new List<int>();
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
                    GUIStyle tagStyle = GetTagStyle();
                    Color prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.85f, 0.92f, 1f, 1f);

                    Vector2 textSize = tagStyle.CalcSize(new GUIContent(words[i]));
                    float tagWidth = textSize.x + 18;

                    EditorGUILayout.BeginHorizontal(tagStyle, GUILayout.Width(tagWidth));
                    EditorGUILayout.LabelField(words[i], GUILayout.MinWidth(tagWidth - 18 - 6), GUILayout.ExpandWidth(false));
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
                words.RemoveAt(removeIndex);
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

        public bool IsWordUsed(string word, S4MSettingsWindow window, out string foundInType, List<string> words)
        {
            word = word.Trim();
            if (string.IsNullOrEmpty(word))
            {
                foundInType = null;
                return false;
            }

            // Vérifie dans les filtres
            foreach (var kvp in window.CommandSettings)
            {
                if (kvp.Value is CommandSettings cmd && cmd.TriggerWords.Contains(word))
                {
                    foundInType = kvp.Key.Name;
                    return true;
                }
                else
                // ColorFilterSettings
                if (kvp.Value is ColorFilterSettings colorFilter)
                {
                    foreach (var entry in colorFilter.Entries)
                    {
                        if (entry.TriggerWords.Contains(word))
                        {
                            foundInType = kvp.Key.Name + " (ColorFilter)";
                            return true;
                        }
                    }
                }
                // AnnotationFilterSettings
                else if (kvp.Value is AnnotationFilterSettings annotationFilter)
                {
                    foreach (var entry in annotationFilter.Entries)
                    {
                        if (entry.TriggerWords.Contains(word))
                        {
                            foundInType = kvp.Key.Name + " (AnnotationFilter)";
                            return true;
                        }
                    }
                }
                // Autres settings avec TriggerWords
                else if (kvp.Value is CommandSettings filterCmd && filterCmd.TriggerWords.Contains(word))
                {
                    foundInType = kvp.Key.Name;
                    return true;
                }
            }

            foundInType = null;
            return false;
        }
    }
}