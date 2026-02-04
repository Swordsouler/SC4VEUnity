using Sc4ve.Multimodality;
using UnityEngine;

namespace Sc4ve.Service
{
    public class TextToSpeechService : IService<TextToSpeechController>
    {
        public TextToSpeechController Instantiate()
        {
            GameObject go = new("Text To Speech");
            GameObject.DontDestroyOnLoad(go);
            return go.AddComponent<TextToSpeechController>();
        }
    }
}