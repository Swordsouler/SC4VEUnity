using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace Sven.Command
{
    /// <summary>
    /// Conteneur ScriptableObject temporaire pour permettre l'édition d'un UnityEvent
    /// via le système de sérialisation de l'éditeur.
    /// </summary>
    public class UnityEventHolder : ScriptableObject
    {
        public UnityEvent Actions;
    }

    [Serializable]
    public class EventCommandEntry
    {
        public List<string> TriggerWords { get; set; } = new();
        public UnityEvent Actions { get; set; } = new();

        [NonSerialized] private TriggerWordsDrawer _triggerWordsDrawer;
        [NonSerialized] private UnityEventHolder _eventHolder;

        public void DrawUI(S4MSettingsWindow window)
        {
            _triggerWordsDrawer ??= new TriggerWordsDrawer("Trigger Words");
            _triggerWordsDrawer.Draw(window, TriggerWords);

            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            // Créer un conteneur temporaire si nécessaire
            if (_eventHolder == null)
            {
                _eventHolder = ScriptableObject.CreateInstance<UnityEventHolder>();
            }
            // Copier l'état actuel de l'UnityEvent dans le conteneur
            _eventHolder.Actions = Actions;

            // Utiliser SerializedObject pour dessiner le champ UnityEvent
            var serializedObject = new SerializedObject(_eventHolder);
            var property = serializedObject.FindProperty("Actions");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(property, true);
            if (EditorGUI.EndChangeCheck())
            {
                // Appliquer les modifications au SerializedObject
                serializedObject.ApplyModifiedProperties();
                // Reporter les modifications sur notre objet de données
                Actions = _eventHolder.Actions;
                window.SaveSettings();
            }
        }

        // S'assurer de nettoyer le ScriptableObject temporaire
        public void OnDestroy()
        {
            if (_eventHolder != null)
            {
                ScriptableObject.DestroyImmediate(_eventHolder);
                _eventHolder = null;
            }
        }
    }

    [Serializable]
    public class EventSettings : BaseCommandSettings
    {
        public List<EventCommandEntry> Entries = new();

        protected override void DrawCustomSettings(S4MSettingsWindow window)
        {
            base.DrawCustomSettings(window);

            EditorGUILayout.LabelField("Custom Commands", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            int removeIndex = -1;
            for (int i = 0; i < Entries.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");
                Entries[i].DrawUI(window);

                if (GUILayout.Button("Remove Command"))
                {
                    removeIndex = i;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            if (removeIndex >= 0)
            {
                if (EditorUtility.DisplayDialog("Confirm Removal", "Are you sure you want to remove this custom command entry?", "Yes", "No"))
                {
                    // Nettoyer avant de supprimer
                    Entries[removeIndex].OnDestroy();
                    Entries.RemoveAt(removeIndex);
                    window.SaveSettings();
                }
            }

            if (GUILayout.Button("Add Custom Command"))
            {
                Entries.Add(new EventCommandEntry());
                window.SaveSettings();
            }
        }

        // S'assurer que toutes les ressources temporaires sont libérées lorsque les paramètres sont déchargés.
        public void OnDisable()
        {
            foreach (var entry in Entries)
            {
                entry.OnDestroy();
            }
        }
    }
}