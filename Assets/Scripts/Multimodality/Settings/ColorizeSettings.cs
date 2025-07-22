using System;
using System.Collections.Generic;
using UnityEditor;

namespace Sven.Command
{
    [Serializable]
    public class ColorizeSettings : CommandSettings
    {
        public List<string> PrefixWords { get; set; } = new();

        [NonSerialized] private TriggerWordsDrawer _prefixWordsDrawer;

        protected override void DrawCustomSettings(S4MSettingsWindow window)
        {
            EditorGUILayout.Space(10);

            // Draw the PrefixWords part, specific to ColorizeSettings.
            _prefixWordsDrawer ??= new TriggerWordsDrawer("Prefix Words");
            _prefixWordsDrawer.Draw(window, PrefixWords);
        }
    }
}