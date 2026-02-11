using System;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable]
    public class SpeechCommand : Command
    {
        private SentenceParameter SentenceParameter => GetParameter<SentenceParameter>();

        public override void Execute()
        {
            _ = TextToSpeechController.GenerateAndPlaySpeech(SentenceParameter.Value);
        }
    }
}