using UnityEngine;
using System;
using Vosk;
using NAudio.Wave;
using System.IO;
using NaughtyAttributes;
using UnityEngine.Events;
using System.Linq;

namespace Sven.Multimodality.Vocal
{
    public enum Language
    {
        English,
        French
    }

    /// <summary>
    /// Speech to text using Vosk
    /// </summary>
    public class SpeechToText : SaveAudioToWav
    {

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
        #endregion

        /// <summary>
        /// The language to use for speech recognition
        /// </summary>
        [SerializeField, DisableIf("IsPlaying")] private Language _selectedLanguage = Language.English;

        [Serializable] public class SentenceCompleteEvent : UnityEvent<string> { }

        /// <summary>
        /// The action to call when a sentence is complete
        /// </summary>
        public SentenceCompleteEvent OnSentenceComplete;

        private VoskRecognizer recognizer;

        /// <summary>
        /// Load the recognizer for the language
        /// </summary>
        /// <param name="language">The language to load</param>
        private void LoadRecognizer(Language language)
        {
            string modelPath = Path.Combine(Application.streamingAssetsPath, language == Language.English ? "vosk-model-en-us-0.22" : "vosk-model-fr-0.22");
            Vosk.Vosk.SetLogLevel(0);
            if (!Directory.Exists(modelPath))
            {
                Debug.LogError("Model directory does not exist: " + modelPath);
                return;
            }
            try
            {
                recognizer = new VoskRecognizer(new Model(modelPath), 16000);
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to initialize VoskRecognizer: " + ex.Message);
                return;
            }
        }

        protected new void Start()
        {
            LoadRecognizer(_selectedLanguage);
            OnSentenceComplete.AddListener((sentence) =>
            {
                //Debug.Log(sentence);
            });
            base.Start();
        }

        /// <summary>
        /// Process audio data
        /// </summary>
        /// <param name="data">The audio data</param>
        /// <param name="channels">The number of channels</param>
        protected override void ProcessAudioData(float[] data, int channels)
        {
            base.ProcessAudioData(data, channels);

            // Log the audio data length and first few samples
            //Debug.Log($"Audio data length: {data.Length}");
            //Debug.Log($"First few samples: {string.Join(", ", data.Take(10))}");

            byte[] wavData = ConvertToWav(data, channels, 16000);
            //Debug.Log($"WAV data length: {string.Join(", ", wavData.Take(10))}");
            //short[] wavData = new short[data.Length];
            //for (int i = 0; i < data.Length; i++) wavData[i] = (short)Math.Floor(data[i] * short.MaxValue);
            //Debug.Log($"WAV data length: {string.Join(", ", wavData.Take(10))}");

            if (recognizer.AcceptWaveform(wavData, wavData.Length))
            {
                var result = recognizer.Result();
                OnSentenceComplete?.Invoke(result);
                Debug.Log($"Recognition result: {result}");
            }
            //else
            //{
            //    var partialResult = recognizer.PartialResult();
            //    Debug.Log($"Partial result: {partialResult}");
            //}
        }

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

        protected new void OnDestroy()
        {
            base.OnDestroy();
            switch (_selectedMode)
            {
                case RecordingMode.Microphone:
                    break;
                case RecordingMode.Environment:
                    audioCapture.OnAudioFilterReadEvent.RemoveListener(ProcessAudioData);
                    break;
            }
            recognizer?.Dispose();
        }
    }
}