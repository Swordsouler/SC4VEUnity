using System;
using System.Collections.Generic;

namespace Sven.Command
{
    [Serializable]
    public class CommandSettings
    {
        private List<string> _triggerWords = new();
        public List<string> TriggerWords
        {
            get => _triggerWords;
            set
            {
                if (_triggerWords == value) return;
                _triggerWords = value;
            }
        }
    }
}