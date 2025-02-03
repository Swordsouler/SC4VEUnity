using UnityEngine;
using System;
using Vosk;
using NAudio.Wave;
using System.IO;
using NaughtyAttributes;
using System.Collections;
using Lucene.Net.Documents;
using System.Text;

namespace Sven.Multimodality.Vocal
{
    public enum Language
    {
        English,
        French
    }

    public enum Mode
    {
        Microphone,
        Environment
    }

    /// <summary>
    /// Speech to text using Vosk
    /// </summary>
    public class SpeechToText : MonoBehaviour
    {
        #region Flags

        private bool IsEnvironmentMode => _selectedMode == Mode.Environment;
        private bool IsPlaying => Application.isPlaying;

        #endregion

        #region Silence Fields
        /// <summary>
        /// The duration of silence to wait for before considering a sentence complete (in milliseconds)
        /// </summary>
        [SerializeField] private int _silenceDuration = 300;
        /// <summary>
        /// The volume history time frame (in milliseconds)
        /// </summary>
        [SerializeField] private int _volumeHistoryTimeFrame = 5000;
        /// <summary>
        /// The delta for silence threshold
        /// </summary> 
        [SerializeField] private int _deltaForSilenceThreshold = 10;
        /// <summary>
        /// Bit depth of the audio samples
        /// </summary>
        [SerializeField] private int _bitsPerSample = 16;
        #endregion

        /// <summary>
        /// The language to use for speech recognition
        /// </summary>
        [SerializeField, DisableIf("IsPlaying")] private Language _selectedLanguage = Language.English;

        /// <summary>
        /// The mode to use for speech recognition
        /// </summary>
        [SerializeField, DisableIf("IsPlaying")] private Mode _selectedMode = Mode.Microphone;

        /// <summary>
        /// The audio listener to use for environment mode
        /// </summary>
        [SerializeField, ShowIf("IsEnvironmentMode"), DisableIf("IsPlaying")] private AudioListener _audioListener;

        /// <summary>
        /// The action to call when a sentence is complete
        /// </summary>
        public event Action<string> OnSentenceComplete;

        private VoskRecognizer recognizer;
        private WaveInEvent waveIn;

        private void Awake()
        {
            OnSentenceComplete += (sentence) =>
            {
                Debug.Log(sentence);
            };
        }

        /// <summary>
        /// Load the recognizer for the language
        /// </summary>
        /// <param name="language">The language to load</param>
        private void LoadRecognizer(Language language)
        {
            string modelPath = Path.Combine(Application.streamingAssetsPath, language == Language.English ? "vosk-model-small-en-us-0.15" : "vosk-model-small-fr-0.22");
            Vosk.Vosk.SetLogLevel(0);
            if (!Directory.Exists(modelPath))
            {
                Debug.LogError("Model directory does not exist: " + modelPath);
                return;
            }
            try
            {
                recognizer = new VoskRecognizer(new Model(modelPath), 16000.0f);
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to initialize VoskRecognizer: " + ex.Message);
                return;
            }
        }

        private void Start()
        {
            LoadRecognizer(_selectedLanguage);

            if (_selectedMode == Mode.Microphone)
            {
                waveIn = new WaveInEvent
                {
                    DeviceNumber = 0,
                    WaveFormat = new WaveFormat(16000, 1)
                };
                waveIn.DataAvailable += OnDataAvailable;
                waveIn.StartRecording();
            }
            else if (_selectedMode == Mode.Environment)
            {
                StartCoroutine(CaptureEnvironmentAudio());
            }
        }

        /// <summary>
        /// Capture audio from the environment
        /// </summary>
        private IEnumerator CaptureEnvironmentAudio()
        {
            yield return new WaitForSeconds(0.1f);
            /*string filePath = Path.Combine(Application.persistentDataPath, "capturedAudio.wav");
            Debug.Log("Capturing audio to: " + filePath);
            using var fileStream = new FileStream(filePath, FileMode.Create);
            using var binaryWriter = new BinaryWriter(fileStream);
            WriteWavHeader(binaryWriter, 0, 16000);

            // save _audioListener data to a file
            while (true)
            {
                yield return new WaitForSeconds(0.1f);
                var audioData = new float[1024];
                _audioListener.GetOutputData(audioData, 0);
                foreach (var sample in audioData)
                {
                    var intSample = (short)(sample * short.MaxValue);
                    binaryWriter.Write(intSample);
                }
            }*/

        }

        /// <summary>
        /// Write the WAV header
        /// </summary>
        /// <param name="bw">BinaryWriter to write the header</param>
        /// <param name="length">Length of the audio data in samples</param>
        /// <param name="sampleRate">Sample rate of the audio data</param>
        /*private void WriteWavHeader(BinaryWriter bw, int length, int sampleRate)
        {
            // Simplification : The WAV header is written here without considering all the details
            // For a complete implementation, include all necessary fields
            bw.Write(new char[4] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + length * 2); // File size
            bw.Write(new char[4] { 'W', 'A', 'V', 'E' });
            bw.Write(new char[4] { 'f', 'm', 't', ' ' });
            bw.Write(16); // Sub chunk size
            bw.Write((short)1); // Audio format
            bw.Write((short)1); // Number of channels
            bw.Write(sampleRate); // Sample rate
            bw.Write(sampleRate * _bitsPerSample / 8); // Byte rate
            bw.Write((short)(_bitsPerSample / 8)); // Block align
            bw.Write((short)_bitsPerSample); // Bits per sample
            bw.Write(new char[4] { 'd', 'a', 't', 'a' });
            bw.Write(length * 2); // Data size
        }*/

        /// <summary>
        /// Capture audio from the environment
        /// </summary>
        /// <param name="sender">The sender of the event</param>
        /// <param name="e">The event arguments</param>
        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
            {
                var result = recognizer.Result();
                OnSentenceComplete?.Invoke(result);
            }
            else
            {
                var partialResult = recognizer.PartialResult();
                Debug.Log(partialResult);
            }
        }

        void OnDestroy()
        {
            if (_selectedMode == Mode.Microphone)
            {
                if (waveIn != null)
                {
                    waveIn.StopRecording();
                    waveIn.Dispose();
                }
            }
            recognizer?.Dispose();
        }
    }
}