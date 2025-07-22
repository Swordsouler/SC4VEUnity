using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Sven.Command
{
    public abstract class BaseSettings<T> where T : BaseSettingsGUI
    {
        protected static Dictionary<Type, T> _settings;
        public List<T> Settings
        {
            get
            {
                if (_settings == null)
                {
                    _settings = new Dictionary<Type, T>();
                    string path = Path.Combine(UnityEngine.Application.streamingAssetsPath, "Multimodality/CommandSettings.json");
                    if (File.Exists(path))
                    {
                        string json = File.ReadAllText(path);
                        var temp = JsonConvert.DeserializeObject<Dictionary<string, T>>(json);
                        foreach (var kvp in temp)
                        {
                            Type type = Type.GetType(kvp.Key);
                            if (type != null)
                                _settings[type] = kvp.Value;
                        }
                    }
                }
                return _settings.TryGetValue(GetType(), out T settings) ? new List<T> { settings } : new List<T>();
            }
        }
    }
}