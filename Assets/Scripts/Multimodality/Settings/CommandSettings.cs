using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sven.Command
{
    [Serializable]
    public class CommandSettings : BaseSettingsGUI
    {
        public List<string> TriggerWords { get; set; } = new();

        [NonSerialized] private TriggerWordsDrawer _triggerWordsDrawer;
        [NonSerialized] private Vector2 _scroll;

        public override void OnGUI(S4MSettingsWindow window)
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // Draw the UI for the base CommandSettings
            _triggerWordsDrawer ??= new TriggerWordsDrawer("Trigger Words");
            _triggerWordsDrawer.Draw(window, TriggerWords);

            // Hook for derived classes to add their own UI elements inside the scroll view
            DrawCustomSettings(window);

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// A hook for derived classes to draw their specific settings inside the scroll view.
        /// </summary>
        protected virtual void DrawCustomSettings(S4MSettingsWindow window)
        {
            // Base implementation is empty.
        }
    }
}