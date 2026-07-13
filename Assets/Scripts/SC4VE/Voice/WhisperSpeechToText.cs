using NaughtyAttributes;
using Sc4ve.Multimodality;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Whisper;

namespace Sc4ve.Voice
{
    public class WhisperSpeechToText : BaseSpeechToText
    {
        [BoxGroup("References"), SerializeField] private WhisperManager _whisperManager;
        [BoxGroup("References"), SerializeField] private VoiceProcessor _voiceProcessor;

        [BoxGroup("Settings"), SerializeField, Tooltip("Démarre l'écoute automatiquement au lancement.")]
        private bool _autoStart = true;

        [BoxGroup("Settings"), SerializeField, Tooltip("Active le mode Push-to-Talk (touche T).")]
        private bool _pushToTalk = false;

        private DateTime _recognizerStartedAt;
        public override DateTime RecognizerStartedAt { get { return _recognizerStartedAt; } }

        private readonly List<float> _audioBuffer = new();
        private DateTime _bufferStartTime;
        private bool _isBuffering;
        private bool _isTranscribing;
        private bool _pttKeyActive;
        private bool _suspended;   // micro ignoré pendant que le système parle (TTS)

        // Prompt initial : amorce Whisper avec des exemples de commandes. Whisper segmente bien
        // mieux les mots liés (« colorie là » au lieu de « colorila ») quand il est amorcé par
        // des phrases naturelles. Localisé : biaise aussi Whisper vers la bonne langue.
        private const string CommandStylePromptFr =
            "Commandes vocales en français pour un environnement 3D. " +
            "Exemples : colorie la pomme en rouge ; mets les citrouilles en bleu ; " +
            "déplace ça ici ; agrandis ça là ; sélectionne tout ; masque la voiture rouge.";
        private const string CommandStylePromptEn =
            "Voice commands in English for a 3D environment. " +
            "Examples: color the apple red; make the pumpkins blue; " +
            "move that here; enlarge that there; select all; hide the red car.";

        // Choix du prompt selon la locale de l'application (UserData.Locale).
        private static string CommandStylePrompt =>
            !string.IsNullOrEmpty(UserData.Locale) && UserData.Locale.StartsWith("en")
                ? CommandStylePromptEn
                : CommandStylePromptFr;

        private void Start()
        {
            // Activer les timestamps au niveau du token (requis pour les horodatages mot à mot)
            if (_whisperManager != null)
            {
                _whisperManager.enableTokens = true;
                _whisperManager.tokensTimestamps = true;
                // Langue de reconnaissance = locale de l'application. Sauf si le modèle est
                // anglais-seul (.en) : forcer une autre langue le casserait → on prévient et on
                // garde le réglage du modèle. (Whisper multilingue : « en » par défaut sinon.)
                if (!string.IsNullOrEmpty(UserData.Locale))
                {
                    bool englishOnly = _whisperManager.ModelPath != null && _whisperManager.ModelPath.Contains(".en");
                    if (englishOnly && !UserData.Locale.StartsWith("en"))
                        Debug.LogWarning($"[Whisper] Modèle anglais seul ({_whisperManager.ModelPath}) incompatible avec la locale '{UserData.Locale}'. Utilise un modèle multilingue (fichier sans .en).");
                    else
                        _whisperManager.language = UserData.Locale;
                }
                // Amorcer dès le départ (avant même le chargement du vocabulaire du domaine,
                // et même en mode LLM où SetGrammar n'est pas appelé).
                if (string.IsNullOrEmpty(_whisperManager.initialPrompt))
                    _whisperManager.initialPrompt = CommandStylePrompt;
            }

            if (_autoStart)
                StartListening();
        }

        private void Update()
        {
            if (!_pushToTalk) return;

            bool keyPressed = Keyboard.current != null && Keyboard.current.tKey.isPressed;

            if (keyPressed && !_pttKeyActive)
            {
                _pttKeyActive = true;
                BeginBuffer();
            }
            else if (!keyPressed && _pttKeyActive)
            {
                _pttKeyActive = false;
                _ = TranscribeBufferAsync();
            }
        }

        public void StartListening()
        {
            _voiceProcessor.OnFrameCaptured += OnFrameCaptured;
            if (!_pushToTalk)
                _voiceProcessor.OnRecordingStop += OnVoiceActivityStopped;
            _voiceProcessor.StartRecording();
            OnStatusUpdated?.Invoke("Whisper : écoute en cours...");
        }

        public void StopListening()
        {
            _voiceProcessor.OnFrameCaptured -= OnFrameCaptured;
            _voiceProcessor.OnRecordingStop -= OnVoiceActivityStopped;
            _voiceProcessor.StopRecording();
        }

        // Ignore le micro pendant que le système parle (et jette l'audio capté entre-temps,
        // souvent la voix de synthèse), pour éviter de re-transcrire le TTS comme une commande.
        public override void SetListeningSuspended(bool suspended)
        {
            _suspended = suspended;
            if (suspended)
            {
                _audioBuffer.Clear();
                _isBuffering = false;
            }
        }

        /// <summary>
        /// Définit le vocabulaire du domaine comme prompt initial pour guider Whisper.
        /// Remplace le mécanisme de grammaire de Vosk.
        /// </summary>
        public override void SetGrammar(List<string> vocabulary)
        {
            if (_whisperManager == null) return;
            // Vocabulaire d'abord, exemples en dernier : si Whisper tronque le prompt (limite ~224
            // tokens), il garde la fin — donc les exemples (clés pour la segmentation) survivent.
            _whisperManager.initialPrompt =
                "Vocabulaire : " + string.Join(", ", vocabulary) + ". " + CommandStylePrompt;
            Debug.Log($"[Whisper] Initial prompt mis à jour ({vocabulary.Count} termes).");
        }

        private void OnFrameCaptured(short[] samples)
        {
            if (_suspended) return;               // le système parle → on ignore le micro
            if (_pushToTalk && !_pttKeyActive) return;

            if (!_isBuffering)
                BeginBuffer();

            foreach (short s in samples)
                _audioBuffer.Add(s / (float)short.MaxValue);
        }

        private void OnVoiceActivityStopped()
        {
            if (_suspended) return;
            if (_isBuffering)
                _ = TranscribeBufferAsync();
        }

        private void BeginBuffer()
        {
            _isBuffering = true;
            _bufferStartTime = DateTime.Now;
        }

        private async Task TranscribeBufferAsync()
        {
            if (_isTranscribing || _audioBuffer.Count == 0)
            {
                _audioBuffer.Clear();
                _isBuffering = false;
                return;
            }

            _isTranscribing = true;
            _isBuffering = false;

            float[] buffer = _audioBuffer.ToArray();
            _audioBuffer.Clear();
            DateTime audioStart = _bufferStartTime;

            OnStatusUpdated?.Invoke("Whisper : transcription...");

            try
            {
                int sampleRate = _voiceProcessor.SampleRate > 0 ? _voiceProcessor.SampleRate : 16000;
                WhisperResult result = await _whisperManager.GetTextAsync(buffer, sampleRate, 1);

                if (result != null && !string.IsNullOrWhiteSpace(result.Result))
                {
                    _recognizerStartedAt = audioStart;
                    string json = BuildVoskCompatibleJson(result);
                    Debug.Log($"[Whisper] Transcription : {result.Result.Trim()}");
                    OnTranscriptionResult?.Invoke(json);
                }
            }
            finally
            {
                // Toujours relâcher le verrou : sinon une exception de transcription
                // bloque définitivement toutes les transcriptions suivantes.
                OnStatusUpdated?.Invoke("Whisper : écoute en cours...");
                _isTranscribing = false;
            }
        }

        // Construit un JSON identique au format Vosk pour que RecognitionResult/Sentence
        // puissent le parser sans modification.
        // Format : { "text": "...", "result": [{"word":"...", "start":0.1, "end":0.5}, ...] }
        // Les timestamps sont en secondes relatives à RecognizerStartedAt (= _bufferStartTime).
        private static string BuildVoskCompatibleJson(WhisperResult result)
        {
            var wordsArray = new JSONArray();
            foreach (var (text, start, end) in ExtractWords(result))
            {
                var w = new JSONObject();
                w["word"] = text;
                w["start"] = (float)start.TotalSeconds;
                w["end"] = (float)end.TotalSeconds;
                wordsArray.Add(w);
            }

            var root = new JSONObject();
            root["text"] = result.Result.Trim();
            root["result"] = wordsArray;
            return root.ToString();
        }

        // Groupe les tokens Whisper en mots en utilisant l'espace comme séparateur.
        // Un token qui commence par un espace indique le début d'un nouveau mot.
        // Fallback : si les tokens sont absents, utilise le segment entier.
        private static List<(string text, TimeSpan start, TimeSpan end)> ExtractWords(WhisperResult result)
        {
            var words = new List<(string, TimeSpan, TimeSpan)>();

            foreach (WhisperSegment segment in result.Segments)
            {
                if (segment.Tokens == null)
                {
                    if (!string.IsNullOrWhiteSpace(segment.Text))
                        words.Add((segment.Text.Trim(), segment.Start, segment.End));
                    continue;
                }

                string currentWord = "";
                TimeSpan wordStart = TimeSpan.Zero;
                TimeSpan wordEnd = TimeSpan.Zero;

                foreach (WhisperTokenData token in segment.Tokens)
                {
                    if (token.IsSpecial) continue;

                    bool startsNewWord = token.Text.StartsWith(" ") && !string.IsNullOrWhiteSpace(currentWord);
                    if (startsNewWord)
                    {
                        words.Add((currentWord.Trim(), wordStart, wordEnd));
                        currentWord = "";
                    }

                    if (string.IsNullOrEmpty(currentWord) && token.Timestamp != null)
                        wordStart = token.Timestamp.Start;

                    currentWord += token.Text;

                    if (token.Timestamp != null)
                        wordEnd = token.Timestamp.End;
                }

                if (!string.IsNullOrWhiteSpace(currentWord))
                    words.Add((currentWord.Trim(), wordStart, wordEnd));
            }

            return words;
        }

        private void OnDestroy()
        {
            StopListening();
        }
    }
}
