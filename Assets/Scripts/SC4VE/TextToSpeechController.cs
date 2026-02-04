using Sc4ve.Service;
using SparkTTS;
using SparkTTS.Models;
using SparkTTS.Utils;
using System.Threading.Tasks;
using UnityEngine;

namespace Sc4ve.Multimodality
{
    [RequireComponent(typeof(AudioSource))]
    public class TextToSpeechController : MonoBehaviour
    {
        private static readonly Service<TextToSpeechController, TextToSpeechService> _instanceService = new();
        private static TextToSpeechController Instance => _instanceService.Instance;

        private CharacterVoice _characterVoice;
        private AudioSource _audioSource;

        private async void Start()
        {
            // Initialize with Performance mode for fastest inference
            CharacterVoiceFactory.Initialize(
                LogLevel.INFO,
                MemoryUsage.Performance,
                ExecutionProvider.CoreML
            );

            // Wait for all models to be ready (Performance mode only)
            await CharacterVoiceFactory.WaitForModelsLoadedAsync();

            // Get reference to AudioSource
            _audioSource = GetComponent<AudioSource>();

            // Get the singleton instance of the factory
            var voiceFactory = CharacterVoiceFactory.Instance;

            // Create a styled voice (gender: male/female, pitch: very_low/low/moderate/high/very_high, speed: very_low/low/moderate/high/very_high)
            _characterVoice = await voiceFactory.CreateFromStyleAsync(
                gender: "female",
                pitch: "moderate",
                speed: "moderate",
                referenceText: "Hello, I am a character voice sample."
            );

            // Generate and play speech
            if (_characterVoice != null)
            {
                await GenerateAndPlaySpeech("Hello, welcome to my game! I'm an on-device TTS voice.");
            }
        }

        public static async Task GenerateAndPlaySpeech(string text)
        {
            if (Instance._characterVoice == null) return;

            AudioClip generatedClip = await Instance._characterVoice.GenerateSpeechAsync(text);

            if (generatedClip != null && Instance._audioSource != null)
            {
                Instance._audioSource.clip = generatedClip;
                Instance._audioSource.Play();
            }
        }

        private void OnDestroy()
        {
            // Clean up resources
            _characterVoice?.Dispose();
            // Note: Don't dispose the factory instance as it's a singleton
        }
    }
}