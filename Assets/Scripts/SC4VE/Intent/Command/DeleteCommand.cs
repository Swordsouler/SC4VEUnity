using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("supprime", "supprimer", "efface", "effacer", "détruis", "détruire",
                       "enlève", "enlever", "retire", "retirer")]
    [Serializable, CommandDescription("Supprime (désactive) les objets sélectionnés. Annulable via UndoCommand. Paramètres: SelectionParameter.")]
    public class DeleteCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            var undoActions = new List<Action>();
            var redoActions = new List<Action>();

            foreach (SemantizationCore obj in objects)
            {
                var captured = obj.gameObject;
                // Désactivation douce (SetActive) plutôt que Destroy → permet l'annulation
                captured.SetActive(false);
                undoActions.Add(() => captured.SetActive(true));
                redoActions.Add(() => captured.SetActive(false));
                Debug.Log($"[Delete] {obj.GetUUID()} désactivé.");
            }

            if (undoActions.Count > 0)
                CommandHistory.Push(
                    () => undoActions.ForEach(a => a()),
                    () => redoActions.ForEach(a => a()));

            return objects;
        }
    }
}
