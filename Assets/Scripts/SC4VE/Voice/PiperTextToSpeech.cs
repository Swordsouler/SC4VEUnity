using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Sc4ve.Voice
{
    [RequireComponent(typeof(AudioSource))]
    public class PiperTextToSpeech : MonoBehaviour
    {
        [BoxGroup("Piper"), SerializeField,
         Tooltip("Chemin vers piper.exe, relatif à StreamingAssets.")]
        private string _piperExePath = "Piper/piper.exe";

        [BoxGroup("Modèles"), SerializeField,
         Tooltip("Modèle .onnx mono-locuteur pour le français (ex: fr_FR-siwis-medium.onnx). " +
                 "Éviter fr_FR-mls (125 locuteurs) : le locuteur par défaut est peu intelligible.")]
        private string _frenchModelPath = "Piper/models/fr_FR-siwis-medium.onnx";

        [BoxGroup("Modèles"), SerializeField,
         Tooltip("Modèle .onnx utilisé pour l'anglais (ex: en_US-lessac-medium.onnx).")]
        private string _englishModelPath = "Piper/models/en_US-lessac-medium.onnx";

        [BoxGroup("Modèles"), SerializeField]
        private Language _language = Language.French;

        public Action OnSpeechStart;
        public Action OnSpeechEnd;

        private AudioSource _audioSource;
        private bool _isSpeaking;
        private readonly Queue<string> _queue = new();

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        /// <summary>
        /// Met le texte en file d'attente et démarre la synthèse si aucune n'est en cours.
        /// </summary>
        public void Speak(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            UnityEngine.Debug.Log($"[Piper] Énoncé : \"{text}\"");
            _queue.Enqueue(text);
            if (!_isSpeaking) _ = ProcessQueueAsync();
        }

        /// <summary>
        /// Vide la file et arrête la lecture en cours.
        /// </summary>
        public void StopSpeaking()
        {
            _queue.Clear();
            _audioSource.Stop();
        }

        /// <summary>
        /// Change la langue active (affecte les prochains appels à Speak).
        /// </summary>
        public void SetLanguage(Language language) => _language = language;

        private async Task ProcessQueueAsync()
        {
            _isSpeaking = true;
            try
            {
                while (_queue.Count > 0)
                {
                    string text = _queue.Dequeue();
                    await SpeakOnceAsync(text);
                }
            }
            finally
            {
                // Toujours relâcher le verrou : sinon une exception fige définitivement la
                // synthèse (la file ne serait plus jamais consommée).
                _isSpeaking = false;
            }
        }

        private async Task SpeakOnceAsync(string text)
        {
            string piperExe  = Path.Combine(Application.streamingAssetsPath, _piperExePath);
            string modelPath = Path.Combine(Application.streamingAssetsPath,
                                            _language == Language.French ? _frenchModelPath : _englishModelPath);
            string outputWav = Path.Combine(Application.temporaryCachePath,
                                            $"piper_{Guid.NewGuid():N}.wav");
            try
            {
                bool ok = await Task.Run(() => RunPiper(piperExe, modelPath, text, outputWav));
                if (!ok || !File.Exists(outputWav)) return;

                AudioClip clip = LoadWav(outputWav);
                if (clip == null) return;

                OnSpeechStart?.Invoke();
                try
                {
                    _audioSource.PlayOneShot(clip);
                    await Task.Delay((int)(clip.length * 1000) + 200);
                }
                finally
                {
                    // OnSpeechEnd doit TOUJOURS suivre OnSpeechStart : l'écoute STT est suspendue
                    // entre les deux (MultimodalityController) et resterait bloquée sinon.
                    OnSpeechEnd?.Invoke();
                    // AudioClip créé à chaque énoncé → libération explicite (fuite mémoire audio sinon).
                    Destroy(clip);
                }
            }
            finally
            {
                if (File.Exists(outputWav)) File.Delete(outputWav);
            }
        }

        // ── Subprocess ──────────────────────────────────────────────────────────

        private static bool RunPiper(string piperExe, string modelPath, string text, string outputFile)
        {
            if (!File.Exists(piperExe))
            {
                UnityEngine.Debug.LogError($"[Piper] Exécutable introuvable : {piperExe}");
                return false;
            }
            if (!File.Exists(modelPath))
            {
                UnityEngine.Debug.LogError($"[Piper] Modèle introuvable : {modelPath}");
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName               = piperExe,
                Arguments              = $"--model \"{modelPath}\" --output_file \"{outputFile}\"",
                UseShellExecute        = false,
                RedirectStandardInput  = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };

            using Process process = Process.Start(psi);
            if (process == null) return false;

            // Envoyer le texte en UTF-8 sur stdin : l'encodage par défaut de StandardInput (ANSI
            // sous Windows) écorche les caractères accentués ('é', 'à', 'ç'…) avant Piper.
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(text);
            process.StandardInput.BaseStream.Write(utf8, 0, utf8.Length);
            process.StandardInput.BaseStream.Flush();
            process.StandardInput.Close();

            // Drainer stderr en continu : si le tampon du pipe (~4 Ko) se remplit, piper se
            // bloque en écriture et ne termine jamais (deadlock jusqu'au timeout).
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            bool finished = process.WaitForExit(10_000); // timeout 10 s
            if (!finished)
            {
                // Sans Kill(), le processus piper survit au timeout (Dispose ne le termine pas).
                try { process.Kill(); } catch (Exception) { /* déjà terminé entre-temps */ }
                process.WaitForExit(2_000);
                UnityEngine.Debug.LogError("[Piper] Timeout (10 s) — processus piper tué.");
                return false;
            }

            if (process.ExitCode != 0)
            {
                string err = stderrTask.Wait(1_000) ? stderrTask.Result : "(stderr indisponible)";
                UnityEngine.Debug.LogError($"[Piper] Erreur (exit {process.ExitCode}) : {err}");
                return false;
            }

            return true;
        }

        // ── Lecture WAV → AudioClip ─────────────────────────────────────────────
        // Piper produit du PCM 16 bits little-endian, mono ou stéréo.

        private static AudioClip LoadWav(string path)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(stream);

                // En-tête RIFF / WAVE
                reader.ReadChars(4); // "RIFF"
                reader.ReadInt32();  // taille du fichier - 8
                reader.ReadChars(4); // "WAVE"

                // Chunk fmt
                reader.ReadChars(4);            // "fmt "
                int fmtSize = reader.ReadInt32();
                reader.ReadInt16();             // format PCM = 1
                int channels   = reader.ReadInt16();
                int sampleRate = reader.ReadInt32();
                reader.ReadInt32();             // byte rate
                reader.ReadInt16();             // block align
                reader.ReadInt16();             // bits per sample
                if (fmtSize > 16) reader.ReadBytes(fmtSize - 16); // octets extra

                // Chunk data (peut être précédé d'autres chunks comme "LIST")
                string chunkId = new string(reader.ReadChars(4));
                while (chunkId != "data")
                {
                    int skip = reader.ReadInt32();
                    reader.ReadBytes(skip);
                    chunkId = new string(reader.ReadChars(4));
                }
                int dataSize = reader.ReadInt32();
                byte[] raw   = reader.ReadBytes(dataSize);

                // PCM 16-bit → float [-1, 1]
                int sampleCount = raw.Length / 2;
                float[] samples = new float[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                {
                    short s  = (short)(raw[i * 2] | (raw[i * 2 + 1] << 8));
                    samples[i] = s / 32768f;
                }

                AudioClip clip = AudioClip.Create("piper_tts",
                                                  sampleCount / channels,
                                                  channels,
                                                  sampleRate,
                                                  false);
                clip.SetData(samples, 0);
                return clip;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[Piper] Impossible de lire le WAV : {e.Message}");
                return null;
            }
        }
    }
}
