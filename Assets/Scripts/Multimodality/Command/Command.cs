using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Sven.Command
{
    public abstract class Command
    {
        protected static Dictionary<Type, CommandSettings> _settings;
        public List<CommandSettings> Settings
        {
            get
            {
                if (_settings == null)
                {
                    _settings = new Dictionary<Type, CommandSettings>();
                    string path = Path.Combine(UnityEngine.Application.streamingAssetsPath, "Multimodality/CommandSettings.json");
                    if (File.Exists(path))
                    {
                        string json = File.ReadAllText(path);
                        var temp = JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(json);
                        foreach (var kvp in temp)
                        {
                            Type type = Type.GetType(kvp.Key);
                            if (type != null)
                                _settings[type] = kvp.Value;
                        }
                    }
                }
                return _settings.TryGetValue(GetType(), out CommandSettings settings) ? new List<CommandSettings> { settings } : new List<CommandSettings>();
            }
        }

        public static Command Interpret(Sentence sentence)
        {
            // return the right Command type based on keyword in sentence
            throw new NotImplementedException("Interpretation logic not implemented yet");
        }
    }
}