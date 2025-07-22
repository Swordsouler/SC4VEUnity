#if UNITY_EDITOR
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
            string[] tabNames = types.ConvertAll(t => t.Name.Replace("AC", "").Replace("Filter", "")).ToArray();
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
            var baseType = commandType;
            while (baseType != null && baseType != typeof(object))
            {
                if (baseType.IsGenericType)
                {
                    var genericDef = baseType.GetGenericTypeDefinition();
                    if (genericDef == typeof(BaseCommand<>) || genericDef == typeof(QueryFilter<>) || genericDef == typeof(ActionCommand<,>))
                    {
                        // The settings argument is now always the first one.
                        var settingsArg = baseType.GetGenericArguments()[0];

                        if (typeof(BaseCommandSettings).IsAssignableFrom(settingsArg))
                            return settingsArg;
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
            _commandSettings.Clear();
            var allTypes = _filterTypes.Concat(_commandTypes);
            Dictionary<string, JObject> savedSettings = null;

            string path = Path.Combine(Application.streamingAssetsPath, "Multimodality/command_settings.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                savedSettings = JsonConvert.DeserializeObject<Dictionary<string, JObject>>(json);
            }

            foreach (var type in allTypes)
            {
                // Determine the correct settings type from the class definition.
                var settingsType = GetSettingsTypeForCommand(type) ?? typeof(CommandSettings);
                var settingsInstance = Activator.CreateInstance(settingsType) as BaseCommandSettings;

                // If there are saved settings, try to populate the instance.
                if (savedSettings != null && savedSettings.TryGetValue(type.FullName, out var jObject))
                {
                    // Populate the newly created instance with data from the JSON.
                    // This is safer than relying on the $type property.
                    JsonConvert.PopulateObject(jObject.ToString(), settingsInstance);
                }

                _commandSettings[type] = settingsInstance;
            }
        }

        public void SaveSettings()
        {
            string dir = Path.Combine(Application.streamingAssetsPath, "Multimodality");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, "command_settings.json");
            var settingsToSave = CommandSettings.ToDictionary(kvp => kvp.Key.FullName, kvp => kvp.Value);
            // We can remove TypeNameHandling.All now, as it's not strictly needed for loading,
            // but keeping it can be useful for debugging or other tools.
            string json = JsonConvert.SerializeObject(settingsToSave, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects
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