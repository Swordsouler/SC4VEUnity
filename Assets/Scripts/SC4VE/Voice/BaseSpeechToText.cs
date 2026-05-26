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
    }
}
