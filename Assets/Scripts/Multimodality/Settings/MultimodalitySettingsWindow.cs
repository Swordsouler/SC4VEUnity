#if UNITY_EDITOR
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Sven.Command
{
    public class MultimodalitySettingsWindow : EditorWindow
    {
        public IReadOnlyDictionary<Type, BaseCommandSettings> CommandSettings => _commandSettings;
        private readonly Dictionary<Type, BaseCommandSettings> _commandSettings = new();
        private readonly List<Type> _filterTypes = new();
        private readonly List<Type> _commandTypes = new();


        private int _selectedMainTab = 0;
        private int _selectedTypeTab = 0;

        [MenuItem("Window/S4M Settings")]
        public static void ShowWindow()
        {
            GetWindow<MultimodalitySettingsWindow>("S4M Settings");
        }

        private void OnEnable()
        {
            LoadAllCommandTypes();
            LoadSettings();
        }

        private void OnGUI()
        {
            DrawMainTabs();
            List<Type> shownTypes = _selectedMainTab == 0 ? _filterTypes : _commandTypes;
            DrawTypeTabs(shownTypes);
            DrawSettingsTabs(shownTypes, _commandSettings, ref _selectedTypeTab);
        }

        private void DrawMainTabs()
        {
            string[] mainTabs = { "Filter", "Action" };
            int prevSelected = _selectedMainTab;
            _selectedMainTab = GUILayout.Toolbar(_selectedMainTab, mainTabs);
            if (_selectedMainTab != prevSelected)
            {
                _selectedTypeTab = 0; // Reset secondary tab when main tab changes
            }
        }

        private void DrawTypeTabs(List<Type> types)
        {
            string[] tabNames = types.ConvertAll(t => t.Name.Replace("Command", "").Replace("Filter", "")).ToArray();
            if (tabNames.Length == 0)
            {
                GUILayout.Label("No available type.");
                return;
            }
            _selectedTypeTab = GUILayout.Toolbar(_selectedTypeTab, tabNames);
        }

        private void DrawSettingsTabs(List<Type> types, Dictionary<Type, BaseCommandSettings> settingsDict, ref int selectedTab)
        {
            if (selectedTab >= 0 && selectedTab < types.Count)
            {
                var type = types[selectedTab];

                if (!settingsDict.ContainsKey(type))
                {
                    var settingsType = GetSettingsTypeForCommand(type) ?? typeof(CommandSettings);
                    settingsDict[type] = Activator.CreateInstance(settingsType) as BaseCommandSettings;
                }

                var setting = settingsDict[type];
                if (setting != null)
                {
                    setting.OnGUI(this);
                }
            }
        }

        /// <summary>
        /// Returns the settings type for a given command/filter type, if possible.
        /// </summary>
        private Type GetSettingsTypeForCommand(Type commandType)
        {
            // If the commandType inherits from BaseCommand<T>, return T if it's a BaseCommandSettings
            var baseType = commandType;
            while (baseType != null && baseType != typeof(object))
            {
                if (baseType.IsGenericType)
                {
                    var genericDef = baseType.GetGenericTypeDefinition();
                    if (genericDef.Name.StartsWith("BaseCommand") || genericDef.Name.StartsWith("QueryFilter"))
                    {
                        var arg = baseType.GetGenericArguments()[0];
                        if (typeof(BaseCommandSettings).IsAssignableFrom(arg))
                            return arg;
                    }
                }
                baseType = baseType.BaseType;
            }
            return null;
        }

        /// <summary>
        /// Returns true if toCheck inherits from the raw generic type generic.
        /// </summary>
        private static bool IsSubclassOfRawGeneric(Type generic, Type toCheck)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (cur == generic)
                    return true;
                toCheck = toCheck.BaseType;
            }
            return false;
        }

        private void LoadSettings()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Multimodality/command_settings.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var allSettings = JsonConvert.DeserializeObject<Dictionary<string, BaseCommandSettings>>(json, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });

                var allTypes = _filterTypes.Concat(_commandTypes);
                foreach (var type in allTypes)
                {
                    if (allSettings != null && allSettings.TryGetValue(type.FullName, out var loadedSetting))
                        _commandSettings[type] = loadedSetting;
                    else
                        _commandSettings[type] = Activator.CreateInstance(GetSettingsTypeForCommand(type) ?? typeof(CommandSettings)) as BaseCommandSettings;
                }
            }
            else
            {
                var allTypes = _filterTypes.Concat(_commandTypes);
                foreach (var type in allTypes)
                {
                    _commandSettings[type] = Activator.CreateInstance(GetSettingsTypeForCommand(type) ?? typeof(CommandSettings)) as BaseCommandSettings;
                }
            }
        }

        public void SaveSettings()
        {
            string dir = Path.Combine(Application.streamingAssetsPath, "Multimodality");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, "command_settings.json");
            var settingsToSave = CommandSettings.ToDictionary(kvp => kvp.Key.FullName, kvp => kvp.Value);
            string json = JsonConvert.SerializeObject(settingsToSave, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
            File.WriteAllText(path, json);

            AssetDatabase.Refresh();
        }

        private void LoadAllCommandTypes()
        {
            _filterTypes.Clear();
            _commandTypes.Clear();
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
                    if (type.IsClass && !type.IsAbstract)
                    {
                        if (IsSubclassOfRawGeneric(typeof(QueryFilter<>), type))
                        {
                            _filterTypes.Add(type);
                        }
                        else if (IsSubclassOfRawGeneric(typeof(BaseCommand<>), type))
                        {
                            _commandTypes.Add(type);
                        }
                    }
                }
            }
        }
    }
}
#endif