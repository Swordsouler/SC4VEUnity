using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sven.Command
{
    [Serializable]
    public class ColorFilterSettings : BaseCommandSettings
    {
        public List<ColorFilterEntry> Entries = new();

        [NonSerialized] private Vector2 _scroll;
        [NonSerialized] private List<float> _buttonHeights = new();

        public override void OnGUI(MultimodalitySettingsWindow window)
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Color filter entries", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            while (_buttonHeights.Count < Entries.Count)
                _buttonHeights.Add(100f);
            while (_buttonHeights.Count > Entries.Count)
                _buttonHeights.RemoveAt(_buttonHeights.Count - 1);

            int removeIndex = -1;
            for (int i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];

                Rect boxRect = EditorGUILayout.BeginVertical();
                EditorGUILayout.BeginHorizontal("box");

                EditorGUILayout.BeginVertical();

                EditorGUI.BeginChangeCheck();
                entry.ColorParameter.Red = EditorGUILayout.Slider("Red", entry.ColorParameter.Red, 0f, 1f);
                entry.ColorParameter.Green = EditorGUILayout.Slider("Green", entry.ColorParameter.Green, 0f, 1f);
                entry.ColorParameter.Blue = EditorGUILayout.Slider("Blue", entry.ColorParameter.Blue, 0f, 1f);
                entry.ColorParameter.Tolerance = EditorGUILayout.Slider("Tolerance", entry.ColorParameter.Tolerance, 0f, 1f);

                if (EditorGUI.EndChangeCheck() && (Event.current.type == EventType.MouseUp || Event.current.type == EventType.Used))
                {
                    window.SaveSettings();
                }

                entry.DrawTriggerWordsUI(window);

                EditorGUILayout.EndVertical();

                Rect lastRect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.Repaint && lastRect.height > 1)
                    _buttonHeights[i] = lastRect.height;

                float buttonHeight = _buttonHeights[i];

                if (GUILayout.Button("×", GUILayout.Width(24), GUILayout.Height(buttonHeight)))
                    removeIndex = i;

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                if (Event.current.type == EventType.Repaint)
                {
                    var color = entry.ColorParameter.MaxColor;
                    int borderSize = 2;

                    EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.y, boxRect.width, borderSize), color);
                    EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.yMax - borderSize, boxRect.width, borderSize), color);
                    EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.y + borderSize, borderSize, boxRect.height - borderSize * 2), color);
                    EditorGUI.DrawRect(new Rect(boxRect.xMax - borderSize, boxRect.y + borderSize, borderSize, boxRect.height - borderSize * 2), color);
                }

                EditorGUILayout.Space();
            }

            if (removeIndex >= 0)
            {
                Entries.RemoveAt(removeIndex);
                window.SaveSettings();
            }

            if (GUILayout.Button("Add color"))
            {
                Entries.Add(new ColorFilterEntry());
                window.SaveSettings();
            }

            EditorGUILayout.EndScrollView();
        }
    }

    [Serializable]
    public class ColorFilterEntry
    {
        public ColorParameter ColorParameter { get; set; } = new();
        public List<string> TriggerWords { get; set; } = new();

        [NonSerialized] private TriggerWordsDrawer _triggerWordsDrawer;

        public void DrawTriggerWordsUI(MultimodalitySettingsWindow window)
        {
            _triggerWordsDrawer ??= new TriggerWordsDrawer("Trigger words");
            _triggerWordsDrawer.Draw(window, TriggerWords);
        }
    }
}