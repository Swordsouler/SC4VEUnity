using Sc4ve.Multimodality;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// Dernier filet du suivi de tête, en casque réel : si la caméra n'a pas bougé d'un
    /// souffle après trois secondes de casque PORTÉ — un suivi vivant produit toujours du
    /// bruit de mesure, un TrackedPoseDriver muet produit exactement zéro —, la pose de
    /// tête est appliquée directement depuis l'API XR classique (InputDevices), qui parle
    /// au subsystem sans passer par le package Input System. Un diagnostic est logué dans
    /// tous les cas : appareils Input System présents, état du TrackedPoseDriver de la
    /// caméra, validité de la pose classique — de quoi remonter à la racine.
    ///
    /// Le pilotage de secours n'écrit que position et rotation LOCALES de la caméra : le
    /// Camera Offset au-dessus reste la propriété du rig — et de HeightGuard, qui recale
    /// la hauteur si la pose de secours arrive au niveau des yeux plutôt qu'au sol.
    /// </summary>
    public class TrackingGuard : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<ServiceProgression>() == null) return;
            if (FindAnyObjectByType<TrackingGuard>() != null) return;
            new GameObject("Garde-suivi").AddComponent<TrackingGuard>();
        }

        private const float WatchSeconds = 3f;
        private const float DeadRotationDegrees = 0.5f;
        private const float DeadMotionMeters = 0.01f;

        private Transform _camera;
        private float _watchStart = -1f;
        private float _rotationAccumulated;
        private float _motionAccumulated;
        private Quaternion _lastRotation;
        private Vector3 _lastPosition;
        private bool _rescuing;
        private bool _concluded;

        private void LateUpdate()
        {
            if (_rescuing) { ApplyRescuePose(); return; }
            if (_concluded || !SimulatorGate.HeadsetRunning()) return;

            if (_camera == null)
            {
                XROrigin origin = FindAnyObjectByType<XROrigin>();
                if (origin == null || origin.Camera == null) return;
                _camera = origin.Camera.transform;
                _lastRotation = _camera.localRotation;
                _lastPosition = _camera.localPosition;
            }

            // La fenêtre ne court que casque SUR LA TÊTE : posé sur la table, il est
            // parfaitement immobile et ferait conclure à tort un suivi mort.
            if (!HeightGuard.UserPresent()) { _watchStart = -1f; return; }

            if (_watchStart < 0f)
            {
                _watchStart = Time.unscaledTime;
                _rotationAccumulated = 0f;
                _motionAccumulated = 0f;
            }

            _rotationAccumulated += Quaternion.Angle(_lastRotation, _camera.localRotation);
            _motionAccumulated += Vector3.Distance(_lastPosition, _camera.localPosition);
            _lastRotation = _camera.localRotation;
            _lastPosition = _camera.localPosition;

            if (Time.unscaledTime - _watchStart < WatchSeconds) return;

            bool dead = _rotationAccumulated < DeadRotationDegrees && _motionAccumulated < DeadMotionMeters;
            LogDiagnostic(dead);
            _concluded = true;

            if (dead && TryReadHeadPose(out _, out _))
            {
                _rescuing = true;
                Debug.LogWarning("[Garde-suivi] La caméra n'a pas bougé d'un souffle en trois secondes de " +
                                 "casque porté : le TrackedPoseDriver ne reçoit rien — pilotage de secours " +
                                 "par l'API XR classique.");
            }
        }

        private void ApplyRescuePose()
        {
            if (_camera == null) return;
            if (!TryReadHeadPose(out Vector3 position, out Quaternion rotation)) return;
            _camera.localPosition = position;
            _camera.localRotation = rotation;
        }

        private static bool TryReadHeadPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;

            InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!device.isValid) device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!device.isValid) return false;

            bool ok = device.TryGetFeatureValue(CommonUsages.centerEyePosition, out position)
                      && device.TryGetFeatureValue(CommonUsages.centerEyeRotation, out rotation);
            if (!ok)
                ok = device.TryGetFeatureValue(CommonUsages.devicePosition, out position)
                     && device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
            return ok;
        }

        private void LogDiagnostic(bool dead)
        {
            var report = new StringBuilder("[Garde-suivi] Diagnostic après trois secondes de casque porté :\n");

            report.Append("— Appareils Input System : ");
            foreach (UnityEngine.InputSystem.InputDevice device in UnityEngine.InputSystem.InputSystem.devices)
                report.Append($"{device.displayName} [{device.layout}{Usages(device)}] ; ");
            report.Append('\n');

            var driver = _camera != null
                ? _camera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>()
                : null;
            report.Append(driver == null
                ? "— TrackedPoseDriver (Input System) : ABSENT de la caméra.\n"
                : $"— TrackedPoseDriver : {(driver.enabled ? "actif" : "DÉSACTIVÉ")} ; " +
                  $"position {Describe(driver.positionInput)} ; rotation {Describe(driver.rotationInput)}.\n");

            report.Append(TryReadHeadPose(out Vector3 position, out _)
                ? $"— API XR classique : pose de tête VALIDE (y = {position.y:0.00} m).\n"
                : "— API XR classique : AUCUNE pose de tête.\n");

            report.Append($"— Caméra : {_rotationAccumulated:0.00}° et {_motionAccumulated * 100f:0.0} cm " +
                          $"cumulés → suivi {(dead ? "MORT" : "vivant")}.");
            Debug.Log(report.ToString());
        }

        private static string Usages(UnityEngine.InputSystem.InputDevice device)
        {
            if (device.usages.Count == 0) return string.Empty;
            var text = new StringBuilder(", ");
            for (int i = 0; i < device.usages.Count; i++)
                text.Append(i == 0 ? device.usages[i].ToString() : $"+{device.usages[i]}");
            return text.ToString();
        }

        private static string Describe(UnityEngine.InputSystem.InputActionProperty property)
        {
            UnityEngine.InputSystem.InputAction action = property.action;
            if (action == null) return "(aucune action)";
            return $"({(action.enabled ? "activée" : "INACTIVE")}, {action.controls.Count} contrôle(s))";
        }
    }
}
