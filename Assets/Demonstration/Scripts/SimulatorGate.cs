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
    /// Le subsystem d'affichage peut mettre quelques instants à démarrer : on guette
    /// pendant cinq secondes, puis on conclut au bureau — le simulateur reste, comme avant.
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

        private const float WatchDuration = 5f;
        private float _elapsed;

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;

            if (HeadsetRunning())
            {
                GameObject simulator = GameObject.Find("XR Device Simulator");
                if (simulator != null)
                {
                    simulator.SetActive(false);
                    Debug.Log("[Garde-simulateur] Casque réel détecté : XR Device Simulator désactivé.");
                }
                Destroy(gameObject);
                return;
            }

            if (_elapsed > WatchDuration) Destroy(gameObject);
        }

        /// <summary>Un affichage XR réel tourne-t-il ? Partagé avec HeightGuard.</summary>
        internal static bool HeadsetRunning()
        {
            var displays = new List<UnityEngine.XR.XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (UnityEngine.XR.XRDisplaySubsystem display in displays)
                if (display.running) return true;
            return false;
        }
    }
}
