using System.Linq;
using TMPro;
using UnityEngine;

namespace Sc4ve.Voice
{
    public class SSTResultTMP : MonoBehaviour
    {
        public BaseSpeechToText SpeechToText;
        public TextMeshProUGUI ResultText;

        private void Awake()
        {
            SpeechToText.OnTranscriptionResult += OnTranscriptionResult;
        }

        private void OnDestroy()
        {
            if (SpeechToText != null)
                SpeechToText.OnTranscriptionResult -= OnTranscriptionResult;
        }

        private void OnTranscriptionResult(string obj)
        {
            var result = new RecognitionResult(obj, SpeechToText.RecognizerStartedAt);
            if (result.Phrases.All(p => p.Text == "")) return;

            ResultText.text = "";
            for (int i = 0; i < result.Phrases.Length; i++)
            {
                if (result.Phrases[i].Text == "") continue;
                if (i > 0) ResultText.text += " |";
                ResultText.text += result.Phrases[i].Text;
            }
        }
    }
}
