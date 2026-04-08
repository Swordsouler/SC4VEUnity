using System;
using System.Collections.Generic;

namespace Sven.Command
{
    [Serializable]
    public class CommandSettings : BaseCommandSettings
    {
        public List<string> TriggerWords { get; set; } = new();

        [NonSerialized] private TriggerWordsDrawer _triggerWordsDrawer;

        /// <summary>
        /// A hook for derived classes to draw their specific settings inside the scroll view.
        /// </summary>
        protected override void DrawCustomSettings(S4MSettingsWindow window)
        {
            _triggerWordsDrawer ??= new TriggerWordsDrawer("Trigger Words");
            _triggerWordsDrawer.Draw(window, TriggerWords);
        }
    }
}