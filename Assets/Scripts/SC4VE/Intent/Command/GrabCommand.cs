using Sven.Content;
using Sven.Demo;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("attrape", "attraper", "prends", "prendre", "grab", "saisit", "saisir", "empare", "emparer")]
    [Serializable, CommandDescription("Saisit les objets. Paramètres: SelectionParameter.")]
    public class GrabCommand : Command
    {
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
            => new List<Parameter> { ctx.BuildSelectionParameter(), ctx.BuildGrabPointParameter() };

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects;
            if (objects == null || objects.Count == 0)
            {
                Debug.LogWarning("[Grab] Aucun objet à saisir.");
                return new();
            }

            // Une seule main : on saisit le premier objet ramassable (tag "Pickup")
            // de la sélection, avec exactement le même mécanisme que la touche F.
            foreach (SemantizationCore sc in objects)
            {
                if (!sc.gameObject.CompareTag("Pickup"))
                {
                    Debug.LogWarning($"[Grab] {sc.GetUUID()} n'est pas ramassable (tag != 'Pickup').");
                    continue;
                }
                DemoCharacterController.PickupObjectStatic(sc.gameObject);
                Debug.Log($"[Grab] Objet {sc.GetUUID()} pris en main.");
                return new List<SemantizationCore> { sc };
            }

            Debug.LogWarning("[Grab] Aucun objet ramassable dans la sélection.");
            return new();
        }
    }
}