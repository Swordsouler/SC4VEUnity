using Sven.Content;
using Sven.Demo;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription("Relâche les objets. Paramètres: SelectionParameter.")]
    [RuleBasedTriggers("lâche", "lâcher", "relâche", "relâcher", "pose", "poser", "release", "libère", "libérer", "dépose", "déposer")]
    public class ReleaseCommand : Command
    {
        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;

            // Scène XR : on passe par le XR Interaction Toolkit.
            // Scène bureau (pas de XRInteractionManager) : on retombe sur le contrôleur de démo.
            XRInteractionManager manager = XRGrabSupport.Manager;
            if (manager != null) return ReleaseWithXR(manager, objects);

            foreach (SemantizationCore semantizationCore in objects)
            {
                if (!semantizationCore.TryGetComponent(out Renderer renderer)) continue;
                DemoCharacterController.DropObjectStatic();
            }
            return objects;
        }

        /// <summary>
        /// Relâche chaque objet de la sélection actuellement tenu par un interactor.
        /// </summary>
        private static List<SemantizationCore> ReleaseWithXR(XRInteractionManager manager, List<SemantizationCore> objects)
        {
            foreach (SemantizationCore semantizationCore in objects)
            {
                IXRSelectInteractable interactable = XRGrabSupport.FindInteractable(semantizationCore.gameObject);
                if (interactable == null || !interactable.isSelected) continue;

                // Copie : SelectExit modifie interactorsSelecting pendant l'itération.
                foreach (IXRSelectInteractor interactor in interactable.interactorsSelecting.ToList())
                    manager.SelectExit(interactor, interactable);

                Debug.Log($"[Release] Objet {semantizationCore.GetUUID()} relâché (XR).");
            }
            return objects;
        }
    }
}
