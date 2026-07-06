using Ionic.Zip;
using NaughtyAttributes;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Networking;
using Vosk;

namespace Sc4ve.Voice
{
    public enum Language
    {
        French,
        English,
    }

    public class VoskSpeechToText : BaseSpeechToText
    {
        [BoxGroup("References"), SerializeField, Tooltip("Location of the model, relative to the Streaming Assets folder.")]
        private string _modelPath = "vosk-model-small-en-0.22.zip";
        public string ModelPath
        {
            get => _modelPath;
            set => _modelPath = value;
        }

        [BoxGroup("References"), SerializeField, Tooltip("The source of the microphone input.")]
        private VoiceProcessor _voiceProcessor;
        public VoiceProcessor VoiceProcessor
        {
            get => _voiceProcessor;
            set => _voiceProcessor = value;
        }

        [BoxGroup("References"), SerializeField, Tooltip("The Max number of alternatives that will be processed.")]
        private int _maxAlternatives = 1;
        public int MaxAlternatives
        {
            get => _maxAlternatives;
            set => _maxAlternatives = value;
        }

        [BoxGroup("References"), SerializeField, Tooltip("How long should we record before restarting?")]
        private float _maxRecordLength = 5;
        public float MaxRecordLength
        {
            get => _maxRecordLength;
            set => _maxRecordLength = value;
        }

        [BoxGroup("References"), SerializeField, Tooltip("Should the recognizer start when the application is launched?")]
        private bool _autoStart = true;
        public bool AutoStart
        {
            get => _autoStart;
            set => _autoStart = value;
        }


        [BoxGroup("References"), SerializeField, Tooltip("The phrases that will be detected. If left empty, all words will be detected.")]
        private List<string> _keyPhrases = new();
        public List<string> KeyPhrases
        {
            get => _keyPhrases;
            set => _keyPhrases = value;
        }

        [BoxGroup("Settings"), SerializeField, Tooltip("Enable push to talk with the 'T' key.")]
        private bool _pushToTalk = false;
        public bool PushToTalk
        {
            get => _pushToTalk;
            set => _pushToTalk = value;
        }

        //Cached version of the Vosk Model.
        private Model _model;

        //Cached version of the Vosk recognizer.
        private VoskRecognizer _recognizer;

        //Holds all of the audio data until the user stops talking.
        private readonly List<short> _buffer = new();

        //The absolute path to the decompressed model folder.
        private string _decompressedModelPath;

        //A string that contains the keywords in Json Array format
        private string _grammar = "";

        //Flag that is used to wait for the model file to decompress successfully.
        private bool _isDecompressing;

        //Flag that is used to wait for the the script to start successfully.
        private bool _isInitializing;

        //Flag that is used to check if Vosk was started.
        private bool _didInit;

        //Threading Logic

        // Flag to signal we are ending (volatile : lu par la boucle de fond, écrit par le main thread)
        private volatile bool _running;

        // Tâche de la boucle de reconnaissance en cours, pour pouvoir l'attendre avant Dispose.
        private Task _workerTask;

        //Thread safe queue of microphone data.
        private readonly ConcurrentQueue<short[]> _threadedBufferQueue = new();

        //Thread safe queue of resuts
        private readonly ConcurrentQueue<string> _threadedResultQueue = new();

        //Flag to track if PTT key is currently pressed
        private bool _pttKeyActive = false;

        private DateTime _recognizerInitializedAt;
        public override DateTime RecognizerStartedAt { get { return _recognizerInitializedAt; } }

        private DateTime _recordingStoppedAt;
        private bool _isFirstRecording = true;



        private static readonly ProfilerMarker voskRecognizerCreateMarker = new("VoskRecognizer.Create");
        private static readonly ProfilerMarker voskRecognizerReadMarker = new("VoskRecognizer.AcceptWaveform");

        //If Auto start is enabled, starts vosk speech to text.
        void Start()
        {
            if (AutoStart)
            {
                StartVoskStt(null, default, false, MaxAlternatives);
            }
        }

        /// <summary>
        /// Start Vosk Speech to text
        /// </summary>
        /// <param name="keyPhrases">A list of keywords/phrases. Keywords need to exist in the models dictionary, so some words like "webview" are better detected as two more common words "web view".</param>
        /// <param name="modelPath">The path to the model folder relative to StreamingAssets. If the path has a .zip ending, it will be decompressed into the application data persistent folder.</param>
        /// <param name="startMicrophone">"Should the microphone after vosk initializes?</param>
        /// <param name="maxAlternatives">The maximum number of alternative phrases detected</param>
        public void StartVoskStt(List<string> keyPhrases = null, string modelPath = default, bool startMicrophone = false, int maxAlternatives = 3)
        {
            if (_isInitializing)
            {
                Debug.LogError("Initializing in progress!");
                return;
            }
            if (_didInit)
            {
                Debug.LogError("Vosk has already been initialized!");
                return;
            }

            if (!string.IsNullOrEmpty(modelPath))
            {
                ModelPath = modelPath;
            }

            if (keyPhrases != null)
            {
                KeyPhrases = keyPhrases;
            }

            MaxAlternatives = maxAlternatives;
            StartCoroutine(DoStartVoskStt(startMicrophone));
        }

        //Decompress model, load settings, start Vosk and optionally start the microphone
        private IEnumerator DoStartVoskStt(bool startMicrophone)
        {
            _isInitializing = true;
            yield return WaitForMicrophoneInput();

            yield return Decompress();

            OnStatusUpdated?.Invoke("Loading Model from: " + _decompressedModelPath);
            //Vosk.Vosk.SetLogLevel(0);
            _model = new Model(_decompressedModelPath);

            yield return null;

            // The recognizer is now created upfront
            CreateRecognizer();

            OnStatusUpdated?.Invoke("Initialized");
            VoiceProcessor.OnFrameCaptured += VoiceProcessorOnOnFrameCaptured;
            VoiceProcessor.OnRecordingStop += VoiceProcessorOnOnRecordingStop;

            if (startMicrophone || _pushToTalk)
                VoiceProcessor.StartRecording();

            _isInitializing = false;
            _didInit = true;

            if (!_pushToTalk)
            {
                ToggleRecording();
            }
        }

        //Translates the KeyPhraseses into a json array and appends the `[unk]` keyword at the end to tell vosk to filter other phrases.
        private void UpdateGrammar()
        {
            if (KeyPhrases.Count == 0)
            {
                _grammar = "";
                return;
            }

            JSONArray keywords = new();
            foreach (string keyphrase in KeyPhrases)
            {
                keywords.Add(new JSONString(keyphrase.ToLower()));
            }

            keywords.Add(new JSONString("[unk]"));

            _grammar = keywords.ToString();
        }

        //Decompress the model zip file or return the location of the decompressed files.
        private IEnumerator Decompress()
        {
            if (!Path.HasExtension(ModelPath)
                || Directory.Exists(
                    Path.Combine(Application.persistentDataPath, Path.GetFileNameWithoutExtension(ModelPath))))
            {
                OnStatusUpdated?.Invoke("Using existing decompressed model.");
                _decompressedModelPath =
                    Path.Combine(Application.persistentDataPath, Path.GetFileNameWithoutExtension(ModelPath));

                yield break;
            }

            OnStatusUpdated?.Invoke("Decompressing model...");
            string modelPath = ModelPath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string dataPath = Path.Combine(Application.streamingAssetsPath, modelPath);

            Stream dataStream;
            // Read data from the streaming assets path. You cannot access the streaming assets directly on Android.
            if (dataPath.Contains("://"))
            {
                UnityWebRequest www = UnityWebRequest.Get(dataPath);
                www.SendWebRequest();
                while (!www.isDone)
                {
                    yield return null;
                }
                dataStream = new MemoryStream(www.downloadHandler.data);
            }
            // Read the file directly on valid platforms.
            else
            {
                dataStream = File.OpenRead(dataPath);
            }

            //Read the Zip File
            var zipFile = ZipFile.Read(dataStream);

            //Listen for the zip file to complete extraction
            zipFile.ExtractProgress += ZipFileOnExtractProgress;

            //Update status text
            OnStatusUpdated?.Invoke("Reading Zip file");

            //Start Extraction
            zipFile.ExtractAll(Application.persistentDataPath);

            //Wait until it's complete
            while (_isDecompressing == false)
            {
                yield return null;
            }
            //Override path given in ZipFileOnExtractProgress to prevent crash
            _decompressedModelPath = Path.Combine(Application.persistentDataPath, Path.GetFileNameWithoutExtension(ModelPath));

            //Update status text
            OnStatusUpdated?.Invoke("Decompressing complete!");
            //Wait a second in case we need to initialize another object.
            yield return new WaitForSeconds(1);
            //Dispose the zipfile reader.
            zipFile.Dispose();
        }

        ///The function that is called when the zip file extraction process is updated.
        private void ZipFileOnExtractProgress(object sender, ExtractProgressEventArgs e)
        {
            if (e.EventType == ZipProgressEventType.Extracting_AfterExtractAll)
            {
                _isDecompressing = true;
                _decompressedModelPath = e.ExtractLocation;
            }
        }

        //Wait until microphones are initialized
        private IEnumerator WaitForMicrophoneInput()
        {
            while (Microphone.devices.Length <= 0)
                yield return null;
        }

        //Can be called from a script or a GUI button to start detection.
        public void ToggleRecording()
        {
            if (_pushToTalk)
            {
                Debug.LogWarning("ToggleRecording is disabled when PushToTalk is active.");
                return;
            }

            Debug.Log("Toggle Recording");
            if (!VoiceProcessor.IsRecording)
            {
                Debug.Log("Start Recording");
                VoiceProcessor.StartRecording();
                HandleRecordingStart();
            }
            else
            {
                Debug.Log("Stop Recording");
                VoiceProcessor.StopRecording();
                HandleRecordingStop();
            }
        }

        //Calls the On Phrase Recognized event on the Unity Thread
        void Update()
        {
            if (_threadedResultQueue.TryDequeue(out string voiceResult))
            {
                OnTranscriptionResult?.Invoke(voiceResult);
            }

            if (_pushToTalk)
            {
                bool pttKeyIsPressed = Input.GetKey(KeyCode.T);

                // Key was just pressed
                if (pttKeyIsPressed && !_pttKeyActive)
                {
                    Debug.Log("Start PTT Recording");
                    HandleRecordingStart();
                }
                // Key was just released
                else if (!pttKeyIsPressed && _pttKeyActive)
                {
                    Debug.Log("Stop PTT Recording");
                    HandleRecordingStop();
                }

                _pttKeyActive = pttKeyIsPressed;
            }
        }

        //Callback from the voice processor when new audio is detected
        private void VoiceProcessorOnOnFrameCaptured(short[] samples)
        {
            if (_pushToTalk && !_pttKeyActive)
            {
                // Do not send audio if PTT is not active
                return;
            }

            _threadedBufferQueue.Enqueue(samples);
        }

        //Callback from the voice processor when recording stops
        private void VoiceProcessorOnOnRecordingStop()
        {
            Debug.Log("Stopped");
        }

        private void OnDestroy()
        {
            bool workerExited = StopWorkerAndWait();
            if (VoiceProcessor != null)
            {
                VoiceProcessor.StopRecording();
                VoiceProcessor.OnFrameCaptured -= VoiceProcessorOnOnFrameCaptured;
                VoiceProcessor.OnRecordingStop -= VoiceProcessorOnOnRecordingStop;
            }

            // Boucle pas sortie → le recognizer et le model sont encore utilisés par le thread
            // de fond : les disposer serait un use-after-dispose natif. On préfère la fuite.
            if (!workerExited) return;

            if (_recognizer != null)
            {
                _recognizer.Dispose();
            }
            if (_model != null)
            {
                _model.Dispose();
            }
        }

        //Feeds the autio logic into the vosk recorgnizer
        private async Task ThreadedWork()
        {
            voskRecognizerReadMarker.Begin();

            while (_running)
            {
                if (_threadedBufferQueue.TryDequeue(out short[] voiceResult))
                {
                    if (_recognizer.AcceptWaveform(voiceResult, voiceResult.Length))
                    {
                        var result = _recognizer.Result();
                        _threadedResultQueue.Enqueue(result);
                    }
                }
                else
                {
                    // Wait for some data
                    await Task.Delay(100);
                }
            }

            // Process the final result after the loop finishes
            if (_recognizer != null)
            {
                var finalResult = _recognizer.FinalResult();
                _threadedResultQueue.Enqueue(finalResult);
            }

            voskRecognizerReadMarker.End();
        }

        /// <summary>
        /// Arrête la boucle de reconnaissance et ATTEND sa sortie. Obligatoire avant de
        /// disposer le recognizer natif : la boucle l'utilise depuis un thread de fond
        /// (AcceptWaveform/FinalResult) et un Dispose concurrent provoque un crash natif.
        /// Retourne false si la boucle n'est pas sortie dans le délai : le recognizer est
        /// alors toujours utilisé — ne pas le disposer ni démarrer une nouvelle boucle.
        /// </summary>
        private bool StopWorkerAndWait()
        {
            _running = false;
            if (_workerTask == null) return true;
            try
            {
                // Borné : la boucle sort au pire après un Task.Delay(100) + un AcceptWaveform.
                if (!_workerTask.Wait(2000))
                {
                    Debug.LogError("[Vosk] La boucle de reconnaissance n'est pas sortie dans le délai (2 s).");
                    return false;
                }
            }
            catch (AggregateException e)
            {
                Debug.LogWarning($"[Vosk] La boucle de reconnaissance s'est terminée avec une erreur : {e.InnerException?.Message}");
            }
            _workerTask = null;
            return true;
        }

        private void HandleRecordingStart()
        {
            if (_isFirstRecording)
            {
                _recognizerInitializedAt = DateTime.Now;
                _isFirstRecording = false;
            }
            else
            {
                var pausedDuration = DateTime.Now - _recordingStoppedAt;
                _recognizerInitializedAt = _recognizerInitializedAt.Add(pausedDuration);
            }

            // Attendre la sortie d'une éventuelle boucle précédente : deux boucles simultanées
            // partageraient le même recognizer natif (non thread-safe).
            if (!StopWorkerAndWait()) return;
            _running = true;
            _workerTask = Task.Run(ThreadedWork);
        }

        private void HandleRecordingStop()
        {
            _recordingStoppedAt = DateTime.Now;
            _running = false;
        }

        private void CreateRecognizer()
        {
            voskRecognizerCreateMarker.Begin();
            UpdateGrammar();

            //Only detect defined keywords if they are specified.
            if (string.IsNullOrEmpty(_grammar))
            {
                _recognizer = new VoskRecognizer(_model, 16000.0f);
            }
            else
            {
                _recognizer = new VoskRecognizer(_model, 16000.0f, _grammar);
            }

            _recognizer.SetMaxAlternatives(MaxAlternatives);
            _recognizer.SetWords(true);
            voskRecognizerCreateMarker.End();
        }

        /// <summary>
        /// Met à jour la grammaire Vosk avec la liste de mots fournie et recrée
        /// le recognizer. À appeler après l'initialisation pour restreindre la
        /// reconnaissance au vocabulaire du domaine (commandes, objets, couleurs…).
        ///
        /// Avantage : Vosk ne peut plus fusionner des mots qui ne font pas partie
        /// du vocabulaire (ex: "déplace ça" ne peut plus devenir "déplaça").
        /// </summary>
        public override void SetGrammar(List<string> words)
        {
            if (!_didInit)
            {
                // Stocker pour application à l'init
                KeyPhrases = words;
                return;
            }

            KeyPhrases = words;

            bool wasRunning = _running;
            // Attendre la sortie de la boucle AVANT de disposer : sinon use-after-dispose
            // du recognizer natif sur le thread de fond (crash natif).
            if (!StopWorkerAndWait())
            {
                Debug.LogError("[Vosk] Grammaire non appliquée : la boucle utilise encore le recognizer.");
                return;
            }

            if (_recognizer != null)
            {
                _recognizer.Dispose();
                _recognizer = null;
            }

            CreateRecognizer();

            if (wasRunning)
            {
                _running = true;
                _workerTask = Task.Run(ThreadedWork);
            }

            Debug.Log($"[Vosk] Grammaire mise à jour : {words.Count} mots.");
        }
    }
}