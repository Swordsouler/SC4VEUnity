using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Accès au XR Interaction Toolkit pour GrabCommand / ReleaseCommand.
    /// Manager vaut null quand la scène n'a pas de rig XR (scènes bureau) :
    /// les commandes retombent alors sur DemoCharacterController.
    /// </summary>
    internal static class XRGrabSupport
    {
        /// <summary>Manager de la scène, ou null si la scène n'est pas une scène XR.</summary>
        public static XRInteractionManager Manager => Object.FindAnyObjectByType<XRInteractionManager>();

        /// <summary>
        /// Interactor à qui confier la saisie : le premier libre, sinon le premier trouvé
        /// (celui-ci relâchera son objet courant, comme la main unique du mode bureau).
        /// </summary>
        public static IXRSelectInteractor FindInteractor()
        {
            XRBaseInteractor[] interactors = Object.FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Exclude);
            if (interactors.Length == 0) return null;
            return interactors.FirstOrDefault(i => !i.hasSelection) ?? interactors[0];
        }

        /// <summary>Interactable saisissable porté par l'objet, ou null s'il n'est pas saisissable.</summary>
        public static IXRSelectInteractable FindInteractable(GameObject gameObject)
            => gameObject.GetComponentInChildren<XRGrabInteractable>();
    }
}
