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
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, obj =>
            {
                var captured = obj.gameObject;
                // Désactivation douce (SetActive) plutôt que Destroy → permet l'annulation
                captured.SetActive(false);
                Debug.Log($"[Delete] {obj.GetUUID()} désactivé.");
                return (() => captured.SetActive(true), () => captured.SetActive(false));
            });
        }
    }
}
