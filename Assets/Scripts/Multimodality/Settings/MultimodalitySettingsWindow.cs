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
    public class MultimodalitySettingsWindow : EditorWindow
    {
        private readonly Dictionary<Type, CommandSettings> commandSettings = new();
        private readonly List<Type> commandTypes = new();

        private int selectedMainTab = 0;
        private int selectedCommandTab = 0;
        private string newTriggerWord = "";
        private bool requestFocus = false;
        private string duplicateError = "";
        private Vector2 scroll;

        [MenuItem("Window/SVEN/Multimodality Settings")]
        public static void ShowWindow()
        {
            GetWindow<MultimodalitySettingsWindow>("SVEN Multimodality Settings");
        }

        private void OnEnable()
        {
            LoadAllCommandTypes();
            LoadSettings();
        }

        private void OnGUI()
        {
            DrawMainTabs();
            List<Type> filteredTypes = GetFilteredCommandTypes();
            DrawCommandTabs(filteredTypes);
            DrawSettingsTabs(filteredTypes, commandSettings, ref selectedCommandTab);
        }

        private void DrawMainTabs()
        {
            string[] mainTabs = { "Filter", "Command" };
            int prevSelected = selectedMainTab;
            selectedMainTab = GUILayout.Toolbar(selectedMainTab, mainTabs);
            if (selectedMainTab != prevSelected)
            {
                selectedCommandTab = 0; // Reset secondary tab when main tab changes
            }
        }

        private void DrawCommandTabs(List<Type> filteredTypes)
        {
            string[] tabNames = filteredTypes.ConvertAll(t => t.Name.Replace("Command", "").Replace("Filter", "")).ToArray();
            if (tabNames.Length == 0)
            {
                GUILayout.Label("No available type.");
                return;
            }
            selectedCommandTab = GUILayout.Toolbar(selectedCommandTab, tabNames);
        }

        private List<Type> GetFilteredCommandTypes()
        {
            if (selectedMainTab == 0) // Filter
            {
                return commandTypes.FindAll(t => typeof(QueryFilter).IsAssignableFrom(t));
            }
            else // Command
            {
                return commandTypes.FindAll(t => !typeof(QueryFilter).IsAssignableFrom(t));
            }
        }

        private void DrawSettingsTabs<T>(List<Type> types, Dictionary<Type, T> settingsDict, ref int selectedTab) where T : class
        {
            if (selectedTab >= 0 && selectedTab < types.Count)
            {
                var type = types[selectedTab];

                if (!settingsDict.ContainsKey(type))
                {
                    if (typeof(T) == typeof(CommandSettings))
                        settingsDict[type] = Activator.CreateInstance(typeof(CommandSettings)) as T;
                    else
                        settingsDict[type] = Activator.CreateInstance(typeof(T)) as T;
                }

                var setting = settingsDict[type];
                var triggerWords = (setting as CommandSettings)?.TriggerWords;

                scroll = EditorGUILayout.BeginScrollView(scroll);

                EditorGUILayout.LabelField("Trigger words", EditorStyles.boldLabel);

                bool addRequested = HandleInputField();

                if (addRequested)
                {
                    TryAddTriggerWord(triggerWords, types, settingsDict);
                }

                DrawDuplicateError(types, settingsDict);

                EditorGUILayout.Space();

                DrawTriggerWords(triggerWords, types, settingsDict);

                EditorGUILayout.Space();
                EditorGUILayout.EndScrollView();
            }
        }

        private bool HandleInputField()
        {
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

        private void TryAddTriggerWord<T>(List<string> triggerWords, List<Type> types, Dictionary<Type, T> settingsDict) where T : class
        {
            string trimmed = newTriggerWord.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                var duplicateTypes = GetDuplicateTypes(trimmed, types, settingsDict);
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

        private List<string> GetDuplicateTypes<T>(string word, List<Type> types, Dictionary<Type, T> settingsDict) where T : class
        {
            var duplicateTypes = new List<string>();
            foreach (var kvp in settingsDict)
            {
                if (kvp.Value is BaseSettings s && s.TriggerWords.Contains(word))
                    duplicateTypes.Add(kvp.Key.Name.Replace("Command", "").Replace("Filter", ""));
            }
            return duplicateTypes;
        }

        private void DrawDuplicateError<T>(List<Type> types, Dictionary<Type, T> settingsDict) where T : class
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

        private void DrawTriggerWords<T>(List<string> triggerWords, List<Type> types, Dictionary<Type, T> settingsDict) where T : class
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
            string path = Path.Combine(Application.streamingAssetsPath, "Multimodality/command_settings.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var allSettings = JsonConvert.DeserializeObject<AllSettings>(json, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto
                });

                foreach (var type in commandTypes)
                {
                    if (allSettings != null && allSettings.Commands.TryGetValue(type.FullName, out var loaded))
                        commandSettings[type] = loaded;
                    else
                        commandSettings[type] = new CommandSettings();
                }
            }
            else
            {
                foreach (var type in commandTypes)
                    commandSettings[type] = new CommandSettings();
            }
        }

        private void SaveSettings()
        {
            var allSettings = new AllSettings();

            foreach (var kvp in commandSettings)
            {
                var filtered = new CommandSettings
                {
                    TriggerWords = kvp.Value.TriggerWords.FindAll(w => !string.IsNullOrWhiteSpace(w))
                };
                allSettings.Commands[kvp.Key.FullName] = filtered;
            }

            string dir = Path.Combine(Application.streamingAssetsPath, "Multimodality");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, "command_settings.json");
            string json = JsonConvert.SerializeObject(allSettings, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto
            });
            File.WriteAllText(path, json);

            AssetDatabase.Refresh();
        }

        private void LoadAllCommandTypes()
        {
            commandTypes.Clear();
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
                        commandTypes.Add(type);
                    }
                }
            }
        }
    }

    [Serializable]
    public class AllSettings
    {
        public Dictionary<string, CommandSettings> Commands { get; set; } = new();
    }
}
#endif