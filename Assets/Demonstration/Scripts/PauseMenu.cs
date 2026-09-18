using Sc4ve.Multimodality;
using Sc4ve.Multimodality.Intent;
using UnityEngine;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// Le menu pause du mini-jeu : bouton MENU de la manette gauche (ou Échap au clavier,
    /// simulateur compris), le temps se fige, deux choix — reprendre, ou réinitialiser la
    /// scène. Le second rejoue EXACTEMENT la commande vocale « réinitialise la scène »
    /// (ResetSceneCommand) : c'est l'issue de secours du visiteur qui ne sait plus quoi
    /// dire, et celle de l'exposant entre deux passages.
    ///
    /// Même chair que l'écran de départ (XRPanel), même bootstrap (ServiceProgression
    /// comme marqueur du mini-jeu). Le porteur PERSISTE et écoute le bouton ; le panneau
    /// n'existe qu'ouvert, replacé devant la tête à chaque ouverture. Pendant l'écran de
    /// départ, le bouton est ignoré : rien à mettre en pause, et les deux panneaux se
    /// superposeraient. Le gel passe par timeScale 0 AVEC ListeningTimeScale.Paused levé,
    /// sans quoi le ralenti d'écoute ramènerait le temps vers 1 dès la frame suivante.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<ServiceProgression>() == null) return;
            if (FindAnyObjectByType<PauseMenu>() != null) return;
            new GameObject("Menu pause").AddComponent<PauseMenu>();
        }

        private bool _open;
        private bool _menuWasPressed;
        private bool _triggerWasPressed;

        private void OnEnable()
        {
            // « Réinitialise la scène » PENDANT la pause (le micro écoute toujours) : le
            // menu se ferme, le temps repart, l'écran de départ prend la place.
            ServiceProgression.GameReset += CloseIfOpen;
        }

        private void OnDisable() => ServiceProgression.GameReset -= CloseIfOpen;

        private void Update()
        {
            bool menu = MenuButtonPressed();
            if (menu && !_menuWasPressed) Toggle();
            _menuWasPressed = menu;

            if (!_open) return;

            // La gâchette clique le bouton survolé — même règle que l'écran de départ.
            bool trigger = XRPanel.RightTriggerPressed();
            if (trigger && !_triggerWasPressed) XRPanel.HoveredAction?.Invoke();
            _triggerWasPressed = trigger;
        }

        /// <summary>
        /// Le bouton MENU de la manette gauche (vraie ou simulée — le nom du contrôle varie
        /// selon le profil), ou Échap au clavier : le réflexe universel, toujours disponible
        /// dans le simulateur.
        /// </summary>
        private static bool MenuButtonPressed()
        {
            UnityEngine.InputSystem.XR.XRController left = UnityEngine.InputSystem.XR.XRController.leftHand;
            if (left != null)
            {
                var button = left.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("menuButton")
                             ?? left.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("menu");
                if (button != null && button.isPressed) return true;
            }

            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.escapeKey.isPressed;
        }

        private void Toggle()
        {
            if (_open) { Close(); return; }
            if (ServiceProgression.WaitingForModeChoice) return;
            Open();
        }

        private void Open()
        {
            _open = true;
            Place();
            Build();
            ListeningTimeScale.Paused = true;
            Time.timeScale = 0f;
            Debug.Log("[Menu pause] Ouvert — le temps est figé.");
        }

        private void Close()
        {
            _open = false;
            XRPanel.HoveredAction = null;
            foreach (Transform child in transform) Destroy(child.gameObject);

            ListeningTimeScale.Paused = false;
            ListeningTimeScale listening = FindAnyObjectByType<ListeningTimeScale>();
            if (listening != null) listening.ResetToNormal();
            else Time.timeScale = 1f;
            Debug.Log("[Menu pause] Fermé — le temps reprend.");
        }

        private void CloseIfOpen()
        {
            if (_open) Close();
        }

        /// <summary>Fermer D'ABORD : le reset ramène l'écran de départ, qui doit trouver un
        /// temps qui court et un rayon libre — pas un menu pause par-dessus.</summary>
        private void ResetScene()
        {
            Close();
            Debug.Log("[Menu pause] Réinitialisation demandée au bouton.");
            new ResetSceneCommand().Execute();
        }

        /// <summary>Devant la tête, à la même distance que l'écran de départ — mais SANS
        /// attente de suivi : en pleine partie, le casque est déjà pris en main.</summary>
        private void Place()
        {
            Camera head = Camera.main;
            if (head == null) return;

            Vector3 forward = head.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            transform.position = head.transform.position + forward * 1.35f + Vector3.down * 0.15f;
            transform.rotation = Quaternion.LookRotation(forward);
        }

        private void Build()
        {
            bool french = UserData.Locale == "fr";

            XRPanel.Backdrop(transform);
            XRPanel.Label(transform, new Vector3(0f, 0.24f, -0.01f), "Pause", characterSize: 0.008f);

            XRPanel.Button(transform, new Vector3(-0.28f, -0.06f, 0f), "Reprendre", french
                ? "<b>Reprendre</b>\nLe service continue\noù il en était."
                : "<b>Resume</b>\nService goes on\nwhere it left off.",
                Close);

            XRPanel.Button(transform, new Vector3(0.28f, -0.06f, 0f), "Réinitialiser", french
                ? "<b>Réinitialiser la scène</b>\nTout revient à sa place,\nla salle se vide."
                : "<b>Reset the scene</b>\nEverything returns to its\nplace, the room empties.",
                ResetScene);
        }
    }
}
