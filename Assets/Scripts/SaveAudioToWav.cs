using UnityEngine;
using System.IO;
using NaughtyAttributes;
using NAudio.Wave;
using System;

namespace Sven.Multimodality.Vocal
{
    public enum RecordingMode
    {
        Microphone,
        Environment
    }

    /// <summary>
    /// Save the audio data from an AudioListener to a WAV file
    /// </summary>
    public class SaveAudioToWav : MonoBehaviour
    {
        #region Flags

        private bool IsPlaying => Application.isPlaying;
        private bool IsEnvironmentMode => _selectedMode == RecordingMode.Environment;

        #endregion

        /// <summary>
        /// The mode to use for speech recognition
        /// </summary>
        [SerializeField, DisableIf("IsPlaying")] protected RecordingMode _selectedMode = RecordingMode.Microphone;

        [ShowIf("IsEnvironmentMode")] public AudioCapture audioCapture;
        private string _filePath;
        protected int _sampleRate = 48000;
        private MemoryStream _audioStream;
        private BinaryWriter _audioWriter;
        protected WaveInEvent _waveIn;

        /// <summary>
        /// Record audio from the microphone
        /// </summary>
        public bool recordAudio = false;
        /// <summary>
        /// The name of the file to save the audio to
        /// </summary>
        public string fileName = "recordedAudio";

        /// <summary>
        /// Record audio data
        /// </summary>
        /// <param name="data">Audio data to record</param>
        /// <param name="channels">Number of channels</param>
        protected virtual void ProcessAudioData(float[] data, int channels)
        {
            if (!recordAudio || _audioWriter == null) return;

            for (int i = 0; i < data.Length; i++)
                _audioWriter.Write((short)(data[i] * short.MaxValue));
        }

        protected void Start()
        {
            _filePath = Path.Combine(Application.persistentDataPath, fileName + ".wav");
            _audioStream = new MemoryStream();
            _audioWriter = new BinaryWriter(_audioStream);
            switch (_selectedMode)
            {
                case RecordingMode.Microphone:
                    _waveIn = new WaveInEvent
                    {
                        DeviceNumber = 0,
                        WaveFormat = new WaveFormat(_sampleRate, 1)
                    };
                    _waveIn.DataAvailable += (sender, e) =>
                    {
                        float[] data = new float[e.BytesRecorded / 2];
                        Buffer.BlockCopy(e.Buffer, 0, data, 0, e.BytesRecorded);
                        ProcessAudioData(data, 1);
                    };
                    _waveIn.StartRecording();
                    break;
                case RecordingMode.Environment:
                    if (audioCapture != null) audioCapture.OnAudioFilterReadEvent.AddListener(ProcessAudioData);
                    break;
            }
        }

        protected void OnDestroy()
        {
            SaveWavFile();
            switch (_selectedMode)
            {
                case RecordingMode.Microphone:
                    if (_waveIn != null)
                    {
                        _waveIn.StopRecording();
                        _waveIn.Dispose();
                    }
                    break;
                case RecordingMode.Environment:
                    if (audioCapture != null) audioCapture.OnAudioFilterReadEvent.RemoveListener(ProcessAudioData);
                    break;
            }
        }

        /// <summary>
        /// Save the audio data to a WAV file
        /// </summary>
        private void SaveWavFile()
        {
            if (_audioStream == null || _audioStream.Length == 0) return;

            byte[] wavFile = ConvertToWav(_audioStream.ToArray(), 2, _sampleRate);
            File.WriteAllBytes(_filePath, wavFile);

            Debug.Log("Audio saved to " + _filePath);
        }

        /// <summary>
        /// Convert the audio data to WAV format
        /// </summary>
        /// <param name="data">Audio data to convert</param>
        /// <param name="channels">Number of channels</param>
        /// <param name="sampleRate">Sample rate of the audio data</param>
        /// <returns>The audio data in WAV format</returns> 
        protected byte[] ConvertToWav(byte[] data, int channels, int sampleRate)
        {
            MemoryStream stream = new();
            BinaryWriter writer = new(stream);

            int byteRate = sampleRate * channels * 2;
            int dataSize = data.Length;

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
            writer.Write(data);

            writer.Flush();
            return stream.ToArray();
        }

        protected byte[] ConvertToWav(float[] data, int channels, int sampleRate)
        {
            using var memoryStream = new MemoryStream();
            using (var writer = new WaveFileWriter(memoryStream, new WaveFormat(sampleRate, channels)))
            {
                writer.WriteSamples(data, 0, data.Length);
            }
            return memoryStream.ToArray();
        }
    }
}