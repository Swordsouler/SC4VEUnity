using NaughtyAttributes;
using Sc4ve.Voice;
using Sven.OwlTime;
using UnityEngine;

namespace Sc4ve.Multimodality
{
    public class MultimodalityController : MonoBehaviour
    {
        [BoxGroup("References"), SerializeField] private VoskSpeechToText _voskSpeechToText;

        private void Awake()
        {
            _voskSpeechToText.OnTranscriptionResult += OnTranscriptionResult;
        }

        private void OnTranscriptionResult(string obj)
        {
            var result = new RecognitionResult(obj);
            for (int i = 0; i < result.Phrases.Length; i++)
            {
                if (result.Phrases[i].Text == "") continue;

                Sentence phrase = result.Phrases[i];

                phrase.Start(new Instant(phrase.StartedAt));
                phrase.End(new Instant(phrase.EndedAt));
                phrase.Semanticize();
                /*_commandChain = new CommandChain(_commandExecutionMode, result.Phrases[i], Settings);
                _commandChain.Execute();*/
            }
        }
    }
}