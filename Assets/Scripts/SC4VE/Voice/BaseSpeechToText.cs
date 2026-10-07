using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Voice
{
    public abstract class BaseSpeechToText : MonoBehaviour
    {
        public Action<string> OnStatusUpdated;
        public Action<string> OnTranscriptionResult;

        public abstract DateTime RecognizerStartedAt { get; }

        public abstract void SetGrammar(List<string> words);

        /// <summary>
        /// Suspend (ou reprend) la prise en compte de l'audio. Utilisé pour ignorer le micro
        /// pendant que le système parle (TTS), afin d'éviter une boucle de rétroaction.
        /// </summary>
        public virtual void SetListeningSuspended(bool suspended) { }

        /// <summary>
        /// Ré-applique la locale courante (UserData.Locale) au moteur de reconnaissance —
        /// l'écran de départ peut changer la langue APRÈS le Start du composant.
        /// </summary>
        public virtual void ApplyLocale() { }

        /// <summary>
        /// Les boutons de la manette gauche qui font parler : TOUS, sauf Menu, réservé au menu
        /// pause. Les noms couvrent les deux familles de dispositions (profils OpenXR et
        /// dispositions XR génériques). Une liste plutôt que « tout bouton de la manette » :
        /// une manette XR expose aussi comme boutons isTracked (toujours vrai) et ses capteurs
        /// tactiles (pouce simplement posé), qui ouvriraient le micro sans le moindre appui.
        /// </summary>
        private static readonly string[] TalkButtons =
        {
            "primaryButton", "secondaryButton",        // X, Y
            "triggerPressed", "triggerButton",         // gâchette
            "gripPressed", "gripButton",               // grip
            "thumbstickClicked", "primary2DAxisClick", // clic du joystick
        };

        /// <summary>
        /// L'appui « parler » du push-to-talk, commun aux moteurs : la touche F au clavier
        /// (la SEULE lettre que le XR Device Simulator ne lie pas — T basculait la manette,
        /// V recentrait les périphériques) ou n'importe quel bouton de la manette gauche
        /// (TalkButtons) — en casque, le clavier est hors de portée, et le visiteur n'a pas
        /// à chercher LE bon bouton. L'aide-manettes (ControllerHints) l'affiche.
        /// </summary>
        protected static bool PushToTalkHeld()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.fKey.isPressed) return true;

            UnityEngine.InputSystem.XR.XRController left = UnityEngine.InputSystem.XR.XRController.leftHand;
            if (left == null) return false;
            foreach (string name in TalkButtons)
            {
                var button = left.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>(name);
                if (button != null && button.isPressed) return true;
            }
            return false;
        }
    }
}
