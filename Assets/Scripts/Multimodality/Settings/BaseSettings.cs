using System;
using System.Collections.Generic;

namespace Sven.Command
{
    [Serializable]
    public class BaseSettings
    {
        private List<string> _triggerWords = new();
        public List<string> TriggerWords
        {
            get => _triggerWords ??= new List<string>();
            set => _triggerWords = value ?? new List<string>();
        }

        public BaseSettings()
        {
            _triggerWords = new List<string>();
        }
    }
}