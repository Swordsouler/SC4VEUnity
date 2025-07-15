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
        private readonly Dictionary<Type, BaseCommandSettings> commandSettings = new();
        private readonly Dictionary<Type, BaseCommandSettings> filterSettings = new();
        private readonly List<Type> filterTypes = new();
        private readonly List<Type> commandTypes = new();

        private int selectedMainTab = 0;
        private int selectedTypeTab = 0;

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
            List<Type> shownTypes = selectedMainTab == 0 ? filterTypes : commandTypes;
            DrawTypeTabs(shownTypes);
            DrawSettingsTabs(shownTypes, selectedMainTab == 0 ? filterSettings : commandSettings, ref selectedTypeTab);
        }

        private void DrawMainTabs()
        {
            string[] mainTabs = { "Filter", "Command" };
            int prevSelected = selectedMainTab;
            selectedMainTab = GUILayout.Toolbar(selectedMainTab, mainTabs);
            if (selectedMainTab != prevSelected)
            {
                selectedTypeTab = 0; // Reset secondary tab when main tab changes
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
            selectedTypeTab = GUILayout.Toolbar(selectedTypeTab, tabNames);
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
                    setting.OnGUI(() => SaveSettings());
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
                var allSettings = JsonConvert.DeserializeObject<AllSettings>(json, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });

                foreach (var type in filterTypes)
                {
                    if (allSettings != null && allSettings.Filters.TryGetValue(type.FullName, out var loadedFilter))
                        filterSettings[type] = loadedFilter;
                    else
                        filterSettings[type] = Activator.CreateInstance(GetSettingsTypeForCommand(type) ?? typeof(CommandSettings)) as BaseCommandSettings;
                }
                foreach (var type in commandTypes)
                {
                    if (allSettings != null && allSettings.Commands.TryGetValue(type.FullName, out var loadedCommand))
                        commandSettings[type] = loadedCommand;
                    else
                        commandSettings[type] = Activator.CreateInstance(GetSettingsTypeForCommand(type) ?? typeof(CommandSettings)) as BaseCommandSettings;
                }
            }
            else
            {
                foreach (var type in filterTypes)
                {
                    filterSettings[type] = Activator.CreateInstance(GetSettingsTypeForCommand(type) ?? typeof(CommandSettings)) as BaseCommandSettings;
                }
                foreach (var type in commandTypes)
                {
                    commandSettings[type] = Activator.CreateInstance(GetSettingsTypeForCommand(type) ?? typeof(CommandSettings)) as BaseCommandSettings;
                }
            }
        }

        private void SaveSettings()
        {
            var allSettings = new AllSettings();

            foreach (var kvp in commandSettings)
            {
                allSettings.Commands[kvp.Key.FullName] = kvp.Value;
            }

            foreach (var kvp in filterSettings)
            {
                allSettings.Filters[kvp.Key.FullName] = kvp.Value;
            }

            string dir = Path.Combine(Application.streamingAssetsPath, "Multimodality");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, "command_settings.json");
            string json = JsonConvert.SerializeObject(allSettings, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
            File.WriteAllText(path, json);

            AssetDatabase.Refresh();
        }

        private void LoadAllCommandTypes()
        {
            filterTypes.Clear();
            commandTypes.Clear();
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
                            filterTypes.Add(type);
                        }
                        else if (IsSubclassOfRawGeneric(typeof(BaseCommand<>), type))
                        {
                            commandTypes.Add(type);
                        }
                    }
                }
            }
        }
    }

    [Serializable]
    public class AllSettings
    {
        public Dictionary<string, BaseCommandSettings> Commands { get; set; } = new();
        public Dictionary<string, BaseCommandSettings> Filters { get; set; } = new();
    }
}
#endif