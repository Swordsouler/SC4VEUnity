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
    }
}
