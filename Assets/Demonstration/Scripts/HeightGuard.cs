using Sc4ve.Multimodality;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// Filet de sécurité de la hauteur des yeux, en casque RÉEL seulement : certains
    /// couples runtime + boundary annoncent un suivi « au sol » — le rig remet alors son
    /// offset artificiel à zéro — tout en rapportant des poses à hauteur d'yeux, et le
    /// joueur se retrouve les yeux au plancher. Quand le casque est PORTÉ (capteur de
    /// présence) et que la hauteur des yeux reste hors du plausible pendant deux secondes,
    /// le Camera Offset du rig est recalé pour ramener les yeux à 1,62 m.
    ///
    /// La correction vise une hauteur CIBLE, pas un cumul : si le vrai suivi revient, la
    /// hauteur mesurée redevient plausible et plus rien n'est touché — et si elle sort à
    /// nouveau des bornes (trop haut, cette fois), le même recalage la résorbe. Le délai
    /// de deux secondes évite de « corriger » un joueur simplement accroupi.
    /// </summary>
    public class HeightGuard : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<ServiceProgression>() == null) return;
            if (FindAnyObjectByType<HeightGuard>() != null) return;
            new GameObject("Garde-hauteur").AddComponent<HeightGuard>();
        }

        private const float TargetEyeHeight = 1.62f;
        private const float MinPlausible = 0.7f;
        private const float MaxPlausible = 2.4f;
        private const float PatienceSeconds = 2f;

        private XROrigin _origin;
        private float _outOfRangeSince = -1f;

        private void Update()
        {
            if (!SimulatorGate.HeadsetRunning()) return;

            if (_origin == null)
            {
                _origin = FindAnyObjectByType<XROrigin>();
                if (_origin == null) return;
            }
            if (_origin.CameraFloorOffsetObject == null) return;

            float eyeHeight = _origin.CameraInOriginSpaceHeight;
            bool implausible = eyeHeight < MinPlausible || eyeHeight > MaxPlausible;

            if (!implausible || !UserPresent())
            {
                _outOfRangeSince = -1f;
                return;
            }

            // Temps NON ralenti : la garde doit agir même pendant le menu pause.
            if (_outOfRangeSince < 0f) _outOfRangeSince = Time.unscaledTime;
            if (Time.unscaledTime - _outOfRangeSince < PatienceSeconds) return;

            float delta = TargetEyeHeight - eyeHeight;
            _origin.CameraFloorOffsetObject.transform.localPosition += Vector3.up * delta;
            _outOfRangeSince = -1f;
            Debug.Log($"[Garde-hauteur] Yeux à {eyeHeight:0.00} m : Camera Offset recalé de {delta:+0.00;-0.00} m.");
        }

        /// <summary>
        /// Le casque est-il sur la tête ? Sans capteur de présence exposé, on suppose que
        /// oui — l'affichage tourne, et le délai de patience fait le reste. La garde évite
        /// de recaler sur un casque POSÉ (à hauteur de table), qui remonterait le joueur
        /// au plafond quand il l'enfile.
        /// </summary>
        private static bool UserPresent()
        {
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.isValid) return true;
            return !head.TryGetFeatureValue(CommonUsages.userPresence, out bool present) || present;
        }
    }
}
