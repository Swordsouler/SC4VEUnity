using System;
using UnityEngine;
using UnityEngine.Events;

namespace Sven.Multimodality.Vocal
{
    /// <summary>
    /// Capture l'audio et déclenche un événement avec les données audio.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public class AudioCapture : MonoBehaviour
    {
        [Serializable]
        public class AudioFilterReadEvent : UnityEvent<float[], int> { }

        public AudioFilterReadEvent OnAudioFilterReadEvent;

        void OnAudioFilterRead(float[] data, int channels)
        {
            OnAudioFilterReadEvent?.Invoke(data, channels);
        }
    }
}