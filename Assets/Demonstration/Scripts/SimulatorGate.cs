using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// Le casque RÉEL a priorité sur le simulateur : quand un affichage XR tourne (Quest en
    /// Link, casque OpenXR…), le « XR Device Simulator » de la scène est désactivé. Actifs
    /// ensemble, ses appareils SIMULÉS — plantés à l'origine, sans hauteur de suivi — se
    /// disputent le rig avec les vrais : la tête retombait au ras du sol (« je suis dans le
    /// sol »), et les manettes lues par l'aide et les menus pouvaient être les simulées.
    /// Sa désactivation retire ses appareils (OnDisable) : le rig retrouve le casque.
    ///
    /// La garde guette toute la session, sans délai limite : avec un Quest 3S en Link, la
    /// session XR a démarré plus de cinq secondes après la scène (la fenêtre d'origine,
    /// mesurée en temps d'image, fond aussi pendant les à-coups du chargement). Le
    /// simulateur était resté actif, avec deux manettes gauches et deux droites : le rayon
    /// et la gâchette ne cliquaient plus l'écran de départ, et l'appui sur X, lu tantôt
    /// sur la vraie manette tantôt sur la simulée, se hachait en appuis trop brefs pour
    /// capter le moindre son. Sans casque, la garde reste simplement en veille.
    /// </summary>
    public class SimulatorGate : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Le nom est garanti par DemoSceneBuilder.EnsureDeviceSimulator — la classe du
            // simulateur, elle, vit dans le paquet XRI : le nom évite la dépendance.
            if (GameObject.Find("XR Device Simulator") == null) return;
            new GameObject("Garde-simulateur").AddComponent<SimulatorGate>();
        }

        private void Update()
        {
            if (!HeadsetRunning()) return;

            GameObject simulator = GameObject.Find("XR Device Simulator");
            if (simulator != null)
            {
                simulator.SetActive(false);
                Debug.Log($"[Garde-simulateur] Casque réel détecté à {Time.realtimeSinceStartup:0.0} s : " +
                          "XR Device Simulator désactivé.");
            }
            Destroy(gameObject);
        }

        // Réutilisée : la garde et HeightGuard/TrackingGuard interrogent à chaque image.
        private static readonly List<UnityEngine.XR.XRDisplaySubsystem> Displays = new();

        /// <summary>Un affichage XR réel tourne-t-il ? Partagé avec HeightGuard et TrackingGuard.</summary>
        internal static bool HeadsetRunning()
        {
            SubsystemManager.GetSubsystems(Displays);
            foreach (UnityEngine.XR.XRDisplaySubsystem display in Displays)
                if (display.running) return true;
            return false;
        }
    }
}
