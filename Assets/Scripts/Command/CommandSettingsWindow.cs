#if UNITY_EDITOR
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Sven.Command
{
    public class CommandSettingsWindow : EditorWindow
    {
        private readonly Dictionary<Type, CommandSettings> settings = new();
        private List<Type> types = new();

        private int selectedTab = 0;
        private string newTriggerWord = "";
        private bool requestFocus = false;
        private string duplicateError = "";
        private Vector2 scroll;

        [MenuItem("Window/SVEN/Command Settings")]
        public static void ShowWindow()
        {
            GetWindow<CommandSettingsWindow>("SVEN Command Settings");
        }

        private void OnEnable()
        {
            LoadAllCommandTypes();
            LoadSettings();
        }

        private void OnGUI()
        {
            DrawTabs();
            EditorGUILayout.Space();

            if (selectedTab >= 0 && selectedTab < types.Count)
            {
                var type = types[selectedTab];
                var setting = settings[type];
                var triggerWords = setting.TriggerWords;

                scroll = EditorGUILayout.BeginScrollView(scroll);

                EditorGUILayout.LabelField("Trigger words", EditorStyles.boldLabel);

                bool addRequested = HandleInputField();

                if (addRequested)
                {
                    TryAddTriggerWord(triggerWords);
                }

                DrawDuplicateError();

                EditorGUILayout.Space();

                DrawTriggerWords(triggerWords);

                EditorGUILayout.Space();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawTabs()
        {
            string[] tabNames = types.ConvertAll(t => t.Name.Replace("Command", "")).ToArray();
            selectedTab = GUILayout.Toolbar(selectedTab, tabNames);
        }

        private bool HandleInputField()
        {
            // Gestion de l'appui sur Entrée
            Event e = Event.current;
            bool addRequested = false;
            if (e.type == EventType.KeyDown &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "NewTriggerWordField")
            {
                addRequested = true;
                e.Use();
            }

            if (requestFocus)
            {
                EditorApplication.delayCall += () =>
                {
                    EditorGUI.FocusTextInControl("NewTriggerWordField");
                };
                requestFocus = false;
            }

            EditorGUILayout.BeginHorizontal();
            GUI.SetNextControlName("NewTriggerWordField");
            newTriggerWord = EditorGUILayout.TextField(newTriggerWord);

            if (GUILayout.Button("Add", GUILayout.Width(60)))
            {
                addRequested = true;
            }
            EditorGUILayout.EndHorizontal();

            return addRequested;
        }

        private void TryAddTriggerWord(List<string> triggerWords)
        {
            string trimmed = newTriggerWord.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                var duplicateTypes = GetDuplicateTypes(trimmed);
                if (duplicateTypes.Count > 0)
                {
                    duplicateError = $"Word \"{newTriggerWord}\" already used in: {string.Join(", ", duplicateTypes)}";
                }
                else
                {
                    triggerWords.Add(trimmed);
                    SaveSettings();
                    duplicateError = "";
                }
            }
            newTriggerWord = "";
            requestFocus = true;
            Repaint();
        }

        private List<string> GetDuplicateTypes(string word)
        {
            var duplicateTypes = new List<string>();
            foreach (var kvp in settings)
            {
                if (kvp.Value.TriggerWords.Contains(word))
                    duplicateTypes.Add(kvp.Key.Name.Replace("Command", ""));
            }
            return duplicateTypes;
        }

        private void DrawDuplicateError()
        {
            if (!string.IsNullOrEmpty(duplicateError))
            {
                GUIStyle errorStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = Color.red }
                };
                EditorGUILayout.LabelField(duplicateError, errorStyle);
            }
        }

        private void DrawTriggerWords(List<string> triggerWords)
        {
            float viewWidth = position.width - 40;
            List<List<int>> lines = new();
            List<int> currentLine = new();
            float currentLineWidth = 0;

            for (int i = 0; i < triggerWords.Count; i++)
            {
                string tagText = triggerWords[i];
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
                    string tagText = triggerWords[i];
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
                triggerWords.RemoveAt(removeIndex);
                SaveSettings();
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

        private void LoadSettings()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Multimodality/CommandSettings.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var temp = JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(json);
                foreach (var type in types)
                {
                    if (temp != null && temp.TryGetValue(type.FullName, out var loaded))
                        settings[type] = loaded;
                    else
                        settings[type] = new CommandSettings();
                }
            }
            else
            {
                foreach (var type in types)
                    settings[type] = new CommandSettings();
            }
        }

        private void SaveSettings()
        {
            var dict = new Dictionary<string, CommandSettings>();
            foreach (var kvp in settings)
            {
                var filtered = new CommandSettings
                {
                    TriggerWords = kvp.Value.TriggerWords.FindAll(w => !string.IsNullOrWhiteSpace(w))
                };
                dict[kvp.Key.FullName] = filtered;
            }

            string json = JsonConvert.SerializeObject(dict, Formatting.Indented);
            string dir = Path.Combine(Application.streamingAssetsPath, "Multimodality");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, "CommandSettings.json");
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
        }

        private void LoadAllCommandTypes()
        {
            types.Clear();
            var baseType = typeof(Command);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] assemblyTypes;
                try
                {
                    assemblyTypes = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    assemblyTypes = e.Types;
                }
                foreach (var type in assemblyTypes)
                {
                    if (type == null) continue;
                    if (type.IsClass && !type.IsAbstract && baseType.IsAssignableFrom(type) && type != baseType)
                    {
                        types.Add(type);
                    }
                }
            }
        }
    }
}
#endif