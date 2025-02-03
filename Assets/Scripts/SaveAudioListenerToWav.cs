using UnityEngine;
using System.IO;

namespace Sven.Multimodality.Vocal
{
    /// <summary>
    /// Save the audio data from an AudioListener to a WAV file
    /// </summary>
    public class SaveAudioListenerToWav : MonoBehaviour
    {
        public AudioCapture audioCapture;
        public string fileName = "recordedAudio";
        private string _filePath;
        private int _sampleRate = 44100;
        private bool _isRecording = false;
        private MemoryStream _audioStream;
        private BinaryWriter _audioWriter;

        private void Awake()
        {
            if (audioCapture != null) audioCapture.OnAudioFilterReadEvent.AddListener(RecordAudioData);
        }

        /// <summary>
        /// Record audio data
        /// </summary>
        /// <param name="data">Audio data to record</param>
        /// <param name="channels">Number of channels</param>
        private void RecordAudioData(float[] data, int channels)
        {
            if (!_isRecording) return;

            for (int i = 0; i < data.Length; i++)
                _audioWriter.Write((short)(data[i] * short.MaxValue));
        }

        private void Start()
        {
            _filePath = Path.Combine(Application.persistentDataPath, fileName + ".wav");
            _audioStream = new MemoryStream();
            _audioWriter = new BinaryWriter(_audioStream);
            StartRecording();
        }

        private void OnDestroy()
        {
            StopRecording();
            SaveWavFile();
            if (audioCapture != null) audioCapture.OnAudioFilterReadEvent.RemoveListener(RecordAudioData);
        }

        private void StartRecording()
        {
            _isRecording = true;
        }

        private void StopRecording()
        {
            _isRecording = false;
        }

        /// <summary>
        /// Save the audio data to a WAV file
        /// </summary>
        private void SaveWavFile()
        {
            if (_audioStream.Length == 0)
            {
                Debug.LogWarning("No audio data to save.");
                return;
            }

            byte[] wavFile = ConvertToWav(_audioStream.ToArray(), 2, _sampleRate);
            File.WriteAllBytes(_filePath, wavFile);

            Debug.Log("Audio saved to " + _filePath);
        }

        /// <summary>
        /// Convert the audio data to WAV format
        /// </summary>
        /// <param name="audioData">Audio data to convert</param>
        /// <param name="channels">Number of channels</param>
        /// <param name="sampleRate">Sample rate of the audio data</param>
        /// <returns>The audio data in WAV format</returns> 
        private byte[] ConvertToWav(byte[] audioData, int channels, int sampleRate)
        {
            MemoryStream stream = new();
            BinaryWriter writer = new(stream);

            int byteRate = sampleRate * channels * 2;
            int dataSize = audioData.Length;

            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + dataSize);
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write("data".ToCharArray());
            writer.Write(dataSize);
            writer.Write(audioData);

            writer.Flush();
            return stream.ToArray();
        }
    }
}