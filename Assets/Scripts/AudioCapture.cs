using System;
using System.IO;
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
        [Serializable] public class AudioFilterReadEvent : UnityEvent<float[], int> { }
        [Serializable] public class AudioFilterReadWavEvent : UnityEvent<byte[]> { }
        public AudioFilterReadEvent OnAudioFilterReadEvent;
        public AudioFilterReadWavEvent OnAudioFilterReadWavEvent;

        private int sampleRate;

        void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            OnAudioFilterReadEvent?.Invoke(data, channels);
            byte[] wavData = ConvertToWav(data, channels, sampleRate);
            OnAudioFilterReadWavEvent?.Invoke(wavData);
        }

        private byte[] ConvertToWav(float[] data, int channels, int sampleRate)
        {
            MemoryStream stream = new();
            BinaryWriter writer = new(stream);

            int byteRate = sampleRate * channels * 2;

            // RIFF header
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + data.Length * 2);
            writer.Write("WAVE".ToCharArray());

            // fmt subchunk
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);

            // data subchunk
            writer.Write("data".ToCharArray());
            writer.Write(data.Length * 2);

            // Write the audio data
            foreach (var sample in data)
            {
                short intSample = (short)(sample * short.MaxValue);
                writer.Write(intSample);
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}