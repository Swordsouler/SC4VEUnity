using System;
using System.Collections.Generic;

namespace Sven.Command
{
    [Serializable]
    public class ColorizeSettings : BaseCommandSettings
    {
        public List<string> PrefixWords { get; set; } = new();

        [NonSerialized] private TriggerWordsDrawer _prefixWordsDrawer;

        protected override void DrawCustomSettings(S4MSettingsWindow window)
        {
            base.DrawCustomSettings(window);
            // Draw the PrefixWords part, specific to ColorizeSettings.
            _prefixWordsDrawer ??= new TriggerWordsDrawer("Prefix Words");
            _prefixWordsDrawer.Draw(window, PrefixWords);
        }
    }
}