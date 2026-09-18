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
        /// L'appui « parler » du push-to-talk, commun aux moteurs : la touche F au clavier
        /// (la SEULE lettre que le XR Device Simulator ne lie pas — T basculait la manette,
        /// V recentrait les périphériques) ou le bouton PRIMAIRE de la manette gauche
        /// (X sur une Quest) — en casque, le clavier est hors de portée. L'aide-manettes
        /// (ControllerHints) affiche ce bouton au-dessus de la manette.
        /// </summary>
        protected static bool PushToTalkHeld()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.fKey.isPressed) return true;

            UnityEngine.InputSystem.XR.XRController left = UnityEngine.InputSystem.XR.XRController.leftHand;
            if (left == null) return false;
            var primary = left.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("primaryButton");
            return primary != null && primary.isPressed;
        }
    }
}
