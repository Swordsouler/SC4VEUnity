using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sven.Command
{

    [Serializable]
    public class EventCommandEntry
    {
        public List<string> TriggerWords { get; set; } = new();
        public EventParameter EventParameter { get; set; } = null!;

        [NonSerialized] private TriggerWordsDrawer _triggerWordsDrawer;

        public void DrawUI(S4MSettingsWindow window)
        {
            _triggerWordsDrawer ??= new TriggerWordsDrawer("Trigger Words");
            _triggerWordsDrawer.Draw(window, TriggerWords);

            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            // EventParameter est un ScriptableObject, nous pouvons l'utiliser pour la sérialisation de l'éditeur.
            if (EventParameter == null)
            {
                EventParameter = ScriptableObject.CreateInstance<EventParameter>();
            }

            // Créer un SerializedObject pour l'instance de EventParameter
            var serializedObject = new SerializedObject(EventParameter);
            // Trouver la propriété pour le champ '_actions'. Cette ligne est maintenant correcte.
            var property = serializedObject.FindProperty("_actions");

            if (property != null)
            {
                EditorGUI.BeginChangeCheck();
                // Dessiner le champ de propriété pour le UnityEvent
                EditorGUILayout.PropertyField(property, true);
                if (EditorGUI.EndChangeCheck())
                {
                    // Appliquer les modifications et sauvegarder
                    serializedObject.ApplyModifiedProperties();
                    window.SaveSettings();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Impossible de trouver la propriété sérialisée pour 'Actions'. Vérifiez le nom du champ dans EventParameter.", MessageType.Error);
            }
        }

        public void OnDestroy()
        {
            if (EventParameter != null)
            {
                ScriptableObject.DestroyImmediate(EventParameter);
                EventParameter = null;
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