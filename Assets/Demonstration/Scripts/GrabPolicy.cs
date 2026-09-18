using Sc4ve.Multimodality;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// La démo se joue à la voix, au geste et au regard — PAS à la main : la saisie au grip
    /// est coupée (allowSelect à faux sur les interactors des mains), un visiteur ne peut
    /// pas emporter une tomate ni renverser la mise en scène. Le SURVOL reste entier : le
    /// rayon continue de désigner (déixis) et d'éclaircir les boutons des panneaux, que la
    /// gâchette clique (XRPanel). Les interactors de TÉLÉPORTATION sont épargnés — couper
    /// leur select serait un autre choix que celui demandé.
    /// </summary>
    public class GrabPolicy : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<ServiceProgression>() == null) return;
            if (FindAnyObjectByType<GrabPolicy>() != null) return;
            new GameObject("Politique de saisie").AddComponent<GrabPolicy>();
        }

        private void Update()
        {
            // Le rig peut se réveiller après nous : on guette jusqu'à trouver au moins un
            // interactor, on coupe, et on s'en va — rien ne change en cours de partie.
            XRBaseInteractor[] interactors = FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Exclude);
            if (interactors.Length == 0) return;

            int muted = 0;
            foreach (XRBaseInteractor interactor in interactors)
            {
                if (interactor.gameObject.name.Contains("Teleport")) continue;
                if (!interactor.allowSelect) continue;
                interactor.allowSelect = false;
                muted++;
            }

            Debug.Log($"[Politique de saisie] Saisie à la main coupée sur {muted} interactor(s) — " +
                      "le survol et la déixis restent entiers.");
            Destroy(gameObject);
        }
    }
}
