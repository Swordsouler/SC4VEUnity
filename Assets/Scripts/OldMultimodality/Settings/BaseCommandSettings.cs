using System;
using UnityEditor;
using UnityEngine;

namespace Sven.Command
{
    [Serializable]
    public class BaseCommandSettings : BaseSettingsGUI
    {
        [NonSerialized] private Vector2 _scroll;

        public override void OnGUI(S4MSettingsWindow window)
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

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