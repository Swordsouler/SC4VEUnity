using Sven.Content;
using System;
using System.Collections.Generic;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable]
    public class SpeechCommand : Command
    {
        private SentenceParameter SentenceParameter => GetParameter<SentenceParameter>();

        public override List<SemantizationCore> Execute()
        {
            _ = TextToSpeechController.GenerateAndPlaySpeech(SentenceParameter.Value);
            return new();
        }
    }
}