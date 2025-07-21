using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sven.Command
{
    [Serializable]
    public class ColorFilterSettings : BaseCommandSettings
    {
        public List<ColorFilterEntry> Entries = new();

        [NonSerialized] private Vector2 _scroll;
        [NonSerialized] private List<float> _buttonHeights = new();

        public override void OnGUI(MultimodalitySettingsWindow window)
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Color filter entries", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            // Avant la boucle for
            while (_buttonHeights.Count < Entries.Count)
                _buttonHeights.Add(100f);
            while (_buttonHeights.Count > Entries.Count)
                _buttonHeights.RemoveAt(_buttonHeights.Count - 1);

            int removeIndex = -1;
            for (int i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];

                Rect boxRect = EditorGUILayout.BeginVertical();
                EditorGUILayout.BeginHorizontal("box");

                EditorGUILayout.BeginVertical();

                // RGB sliders
                EditorGUI.BeginChangeCheck();
                entry.ColorParameter.Red = EditorGUILayout.Slider("Red", entry.ColorParameter.Red, 0f, 1f);
                entry.ColorParameter.Green = EditorGUILayout.Slider("Green", entry.ColorParameter.Green, 0f, 1f);
                entry.ColorParameter.Blue = EditorGUILayout.Slider("Blue", entry.ColorParameter.Blue, 0f, 1f);

                entry.ColorParameter.Tolerance = EditorGUILayout.Slider("Tolerance", entry.ColorParameter.Tolerance, 0f, 1f);

                // Sauvegarde uniquement à la fin de l'édition du slider
                if (EditorGUI.EndChangeCheck() && (Event.current.type == EventType.MouseUp || Event.current.type == EventType.Used))
                {
                    window.SaveSettings();
                }

                // Trigger words
                entry.DrawTriggerWordsUI(window);

                EditorGUILayout.EndVertical();

                // Après EndVertical()
                Rect lastRect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.Repaint && lastRect.height > 1)
                    _buttonHeights[i] = lastRect.height;

                float buttonHeight = _buttonHeights[i];

                if (GUILayout.Button("×", GUILayout.Width(24), GUILayout.Height(buttonHeight)))
                    removeIndex = i;

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                if (Event.current.type == EventType.Repaint)
                {
                    var color = entry.ColorParameter.MaxColor;
                    int borderSize = 2;

                    // Dessiner la bordure
                    EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.y, boxRect.width, borderSize), color); // Haut
                    EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.yMax - borderSize, boxRect.width, borderSize), color); // Bas
                    EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.y + borderSize, borderSize, boxRect.height - borderSize * 2), color); // Gauche
                    EditorGUI.DrawRect(new Rect(boxRect.xMax - borderSize, boxRect.y + borderSize, borderSize, boxRect.height - borderSize * 2), color); // Droite
                }

                EditorGUILayout.Space();
            }

            if (removeIndex >= 0)
            {
                Entries.RemoveAt(removeIndex);
                window.SaveSettings();
            }

            if (GUILayout.Button("Add color"))
            {
                Entries.Add(new ColorFilterEntry());
                window.SaveSettings();
            }

            EditorGUILayout.EndScrollView();
        }
    }

    [Serializable]
    public class ColorFilterEntry
    {
        private string _controlName = System.Guid.NewGuid().ToString();

        private ColorParameter _colorParameter = new();
        public ColorParameter ColorParameter
        {
            get => _colorParameter;
            set => _colorParameter = value ?? new ColorParameter();
        }

        private List<string> _triggerWords = new();
        public List<string> TriggerWords
        {
            get => _triggerWords ??= new List<string>();
            set => _triggerWords = value ?? new List<string>();
        }

        [NonSerialized] private string _newTriggerWord = "";
        [NonSerialized] private bool _requestFocus = false;
        [NonSerialized] private string _duplicateError = "";

        public void DrawTriggerWordsUI(MultimodalitySettingsWindow window)
        {
            EditorGUILayout.LabelField("Trigger words", EditorStyles.boldLabel);

            bool addRequested = HandleInputField();

            if (addRequested)
            {
                TryAddTriggerWord(window);
            }

            if (!string.IsNullOrEmpty(_duplicateError))
            {
                GUIStyle errorStyle = new(EditorStyles.label)
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

            // Ajout par touche Entrée
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
            float viewWidth = EditorGUIUtility.currentViewWidth - 40 - 24;
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