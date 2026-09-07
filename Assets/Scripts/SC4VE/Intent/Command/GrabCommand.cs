using Sven.Content;
using Sven.Demo;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

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

            // Scène XR : on passe par le XR Interaction Toolkit.
            // Scène bureau (pas de XRInteractionManager) : on retombe sur le contrôleur de démo.
            XRInteractionManager manager = XRGrabSupport.Manager;
            if (manager != null) return GrabWithXR(manager, objects);

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

        /// <summary>
        /// Saisit le premier objet de la sélection portant un XRGrabInteractable.
        /// Une seule main comme en mode bureau : si l'interactor tient déjà quelque chose,
        /// il le relâche d'abord.
        /// </summary>
        private static List<SemantizationCore> GrabWithXR(XRInteractionManager manager, List<SemantizationCore> objects)
        {
            IXRSelectInteractor interactor = XRGrabSupport.FindInteractor();
            if (interactor == null)
            {
                Debug.LogError("[Grab] Scène XR sans interactor : aucun XRBaseInteractor trouvé.");
                return new();
            }

            foreach (SemantizationCore sc in objects)
            {
                IXRSelectInteractable interactable = XRGrabSupport.FindInteractable(sc.gameObject);
                if (interactable == null)
                {
                    Debug.LogWarning($"[Grab] {sc.GetUUID()} n'est pas saisissable (pas de XRGrabInteractable).");
                    continue;
                }

                if (interactor.hasSelection)
                    manager.SelectExit(interactor, interactor.interactablesSelected[0]);

                manager.SelectEnter(interactor, interactable);
                Debug.Log($"[Grab] Objet {sc.GetUUID()} pris en main (XR).");
                return new List<SemantizationCore> { sc };
            }

            Debug.LogWarning("[Grab] Aucun objet saisissable dans la sélection.");
            return new();
        }
    }
}