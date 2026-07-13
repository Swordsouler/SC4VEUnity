using Sc4ve.Voice;
using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription("Pose une question de clarification à l'utilisateur via la synthèse vocale. Paramètres: SentenceParameter.")]
    public class SpeechCommand : Command
    {
        private SentenceParameter SentenceParameter => GetParameter<SentenceParameter>();

        public override List<SemantizationCore> Execute()
        {
            string text = SentenceParameter?.Value;
            if (string.IsNullOrWhiteSpace(text)) return new();

            PiperTextToSpeech tts = UnityEngine.Object.FindAnyObjectByType<PiperTextToSpeech>();
            if (tts != null)
                tts.Speak(text);
            else
                Debug.LogWarning("[SpeechCommand] Aucun composant PiperTextToSpeech trouvé dans la scène.");

            Debug.Log($"[SpeechCommand] {text}");
            return new();
        }
    }
}
