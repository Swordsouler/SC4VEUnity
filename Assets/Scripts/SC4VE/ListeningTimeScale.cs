using Sc4ve.Voice;
using UnityEngine;

namespace Sc4ve.Multimodality
{
    /// <summary>
    /// Ralentit le temps de jeu pendant que l'utilisateur parle, pour absorber la latence
    /// du pipeline STT+LLM sans que le joueur soit puni de prendre la parole.
    ///
    /// Se lit comme une pause tactique volontaire, pas comme une béquille — à condition que
    /// la transition soit douce (voir TransitionDuration).
    ///
    /// Mettre ListeningTimeScale à 1 désactive complètement l'effet.
    /// </summary>
    public class ListeningTimeScale : MonoBehaviour
    {
        [SerializeField, Tooltip("Vitesse du temps pendant la parole. 1 = effet désactivé.")]
        [Range(0.1f, 1f)]
        private float _listeningTimeScale = 0.3f;

        [SerializeField, Tooltip("Durée de la transition, en secondes réelles. 0 = instantané.")]
        [Range(0f, 1f)]
        private float _transitionDuration = 0.15f;

        [SerializeField, Tooltip("VoiceProcessor de la scène. Renseigné automatiquement si vide.")]
        private VoiceProcessor _voiceProcessor;

        /// <summary>Vitesse du temps pendant la parole. 1 désactive l'effet.</summary>
        public float Scale
        {
            get => _listeningTimeScale;
            set => _listeningTimeScale = Mathf.Clamp(value, 0.1f, 1f);
        }

        /// <summary>
        /// Vrai pendant le menu pause, qui fige le temps (timeScale 0) : tant que c'est
        /// levé, le ralenti d'écoute ne touche plus à l'horloge — sans cette garde, Update
        /// ramènerait le temps vers sa cible dès la frame suivante et « dé-pauserait ».
        /// </summary>
        public static bool Paused;

        private float _defaultFixedDeltaTime;
        private float _target = 1f;

        private void Awake()
        {
            _defaultFixedDeltaTime = Time.fixedDeltaTime;
            if (_voiceProcessor == null) _voiceProcessor = FindAnyObjectByType<VoiceProcessor>();
        }

        private void OnEnable()
        {
            if (_voiceProcessor == null)
            {
                Debug.LogWarning("[ListeningTimeScale] Aucun VoiceProcessor : le ralenti reste inactif.");
                return;
            }
            _voiceProcessor.OnSpeechStart += StartListening;
            _voiceProcessor.OnRecordingStop += StopListening;
        }

        private void OnDisable()
        {
            if (_voiceProcessor != null)
            {
                _voiceProcessor.OnSpeechStart -= StartListening;
                _voiceProcessor.OnRecordingStop -= StopListening;
            }
            Apply(1f);
        }

        private void StartListening() => _target = _listeningTimeScale;

        private void StopListening() => _target = 1f;

        /// <summary>Retour immédiat au temps normal, fixedDeltaTime compris — la reprise
        /// du menu pause, qui peut interrompre un ralenti en cours de transition.</summary>
        public void ResetToNormal() => Apply(1f);

        private void Update()
        {
            if (Paused) return;
            if (Mathf.Approximately(Time.timeScale, _target)) return;

            float next = _transitionDuration <= 0f
                ? _target
                : Mathf.MoveTowards(Time.timeScale, _target, Time.unscaledDeltaTime / _transitionDuration);
            Apply(next);
        }

        /// <summary>
        /// fixedDeltaTime suit timeScale, sinon la physique se met à saccader au ralenti.
        /// </summary>
        private void Apply(float scale)
        {
            Time.timeScale = scale;
            Time.fixedDeltaTime = _defaultFixedDeltaTime * scale;
        }
    }
}
