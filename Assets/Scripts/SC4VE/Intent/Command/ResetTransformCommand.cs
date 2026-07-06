using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("réinitialise", "réinitialiser", "remet en place", "position d'origine",
                       "état d'origine", "reset", "restaure la position")]
    [Serializable, CommandDescription("Remet les objets à leur position/rotation/taille d'origine. Paramètres: SelectionParameter.")]
    public class ResetTransformCommand : Command
    {
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, obj =>
            {
                var prevPos   = obj.transform.position;
                var prevRot   = obj.transform.rotation;
                var prevScale = obj.transform.localScale;

                OriginalStateStore.RestoreTransform(obj);

                var newPos   = obj.transform.position;
                var newRot   = obj.transform.rotation;
                var newScale = obj.transform.localScale;

                Debug.Log($"[ResetTransform] {obj.GetUUID()} → position d'origine.");
                return (() => {
                    obj.transform.position   = prevPos;
                    obj.transform.rotation   = prevRot;
                    obj.transform.localScale = prevScale;
                }, () => {
                    obj.transform.position   = newPos;
                    obj.transform.rotation   = newRot;
                    obj.transform.localScale = newScale;
                });
            });
        }
    }
}
