using NaughtyAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sc4ve.Voice
{
    public enum RecordingMode
    {
        Microphone,
        AudioFile
    }

    public class VoiceProcessor : MonoBehaviour
    {
        #region Flags
        public bool IsPlaying => Application.isPlaying;
        public bool IsRecording { get; private set; }

        public bool IsAudioFileMode => _recordingMode == RecordingMode.AudioFile;
        public bool IsMicrophoneMode => _recordingMode == RecordingMode.Microphone;
        #endregion

        [SerializeField, DisableIf("IsPlaying")] private RecordingMode _recordingMode;
        //[SerializeField, ShowIf("IsAudioFileMode")] private AudioCapture _audioCapture;
        [SerializeField, ShowIf("IsMicrophoneMode"), DisableIf("IsPlaying")] private int MicrophoneIndex;
        [SerializeField, ShowIf("IsAudioFileMode"), DisableIf("IsPlaying")] private AudioClip _audioClip;

        public int SampleRate { get; private set; }
        public int FrameLength { get; private set; }

        public event Action<short[]> OnFrameCaptured;
        public event Action OnRecordingStop;
        public event Action OnRecordingStart;
        public List<string> Devices { get; private set; }
        public int CurrentDeviceIndex { get; private set; }
        [ShowNativeProperty]
        public string CurrentDeviceName
        {
            get
            {
                if (CurrentDeviceIndex < 0 || CurrentDeviceIndex >= Microphone.devices.Length || Devices == null || Devices.Count == 0)
                    return string.Empty;
                return Devices[CurrentDeviceIndex];
            }
        }

        [Header("Voice Detection Settings")]
        [SerializeField, Tooltip("The minimum volume to detect voice input for"), Range(0.0f, 1.0f)]
        private float _minimumSpeakingSampleValue = 0.05f;

        [SerializeField, Tooltip("Time in seconds of detected silence before voice request is sent")]
        private float _silenceTimer = 1.0f;

        [SerializeField, Tooltip("Auto detect speech using the volume threshold.")]
        private bool _autoDetect;

        private float _timeAtSilenceBegan;
        private bool _audioDetected;
        private bool _didDetect;
        private bool _transmit;

        private event Action RestartRecording;

        private AudioSource _audioSource;

        void Awake()
        {
            UpdateDevices();
        }

#if UNITY_EDITOR
        void Update()
        {
            if (Devices.Count > 0 && CurrentDeviceIndex != MicrophoneIndex)
            {
                ChangeDevice(MicrophoneIndex);
            }
        }
#endif

        /// <summary>
        /// Updates list of available audio devices
        /// </summary>
        public void UpdateDevices()
        {
            if (_recordingMode != RecordingMode.Microphone) return;
            Devices = new List<string>();
            foreach (var device in Microphone.devices)
                Devices.Add(device);

            if (Devices == null || Devices.Count == 0)
            {
                CurrentDeviceIndex = -1;
                Debug.LogError("There is no valid recording device connected");
                return;
            }

            CurrentDeviceIndex = MicrophoneIndex;
        }

        /// <summary>
        /// Change audio recording device
        /// </summary>
        /// <param name="deviceIndex">Index of the new audio capture device</param>
        public void ChangeDevice(int deviceIndex)
        {
            if (_recordingMode != RecordingMode.Microphone) return;
            if (deviceIndex < 0 || deviceIndex >= Devices.Count)
            {
                Debug.LogError(string.Format("Specified device index {0} is not a valid recording device", deviceIndex));
                return;
            }

            if (IsRecording)
            {
                // one time event to restart recording with the new device 
                // the moment the last session has completed
                RestartRecording += () =>
                {
                    CurrentDeviceIndex = deviceIndex;
                    StartRecording(SampleRate, FrameLength);
                    RestartRecording = null;
                };
                StopRecording();
            }
            else
            {
                CurrentDeviceIndex = deviceIndex;
            }
        }

        private Coroutine _recordCoroutine;

        /// <summary>
        /// Start recording audio
        /// </summary>
        /// <param name="sampleRate">Sample rate to record at</param>
        /// <param name="frameSize">Size of audio frames to be delivered</param>
        /// <param name="autoDetect">Should the audio continuously record based on the volume</param>
        public void StartRecording(int sampleRate = 16000, int frameSize = 512, bool? autoDetect = null)
        {
            if (autoDetect != null)
            {
                _autoDetect = (bool)autoDetect;
            }

            switch (_recordingMode)
            {
                case RecordingMode.Microphone:
                    if (IsRecording)
                    {
                        // if sample rate or frame size have changed, restart recording
                        if (sampleRate != SampleRate || frameSize != FrameLength)
                        {
                            RestartRecording += () =>
                            {
                                StartRecording(SampleRate, FrameLength, autoDetect);
                                RestartRecording = null;
                            };
                            StopRecording();
                        }
                        return;
                    }
                    IsRecording = true;
                    SampleRate = sampleRate;
                    FrameLength = frameSize;
                    _audioClip = Microphone.Start(CurrentDeviceName, true, 1, sampleRate);
                    _recordCoroutine = StartCoroutine(RecordMicrophoneData());
                    break;
                case RecordingMode.AudioFile:
                    IsRecording = true;
                    _audioClip = CompressAudioClip(_audioClip, sampleRate);
                    SampleRate = sampleRate;
                    FrameLength = frameSize;
                    _recordCoroutine = StartCoroutine(RecordAudioFileData());
                    break;
            }
        }

        /// <summary>
        /// Compresses an audio clip to a new sample rate
        /// </summary>
        /// <param name="audioClip"></param>
        /// <param name="sampleRate"></param>
        /// <returns></returns>
        public AudioClip CompressAudioClip(AudioClip audioClip, int sampleRate)
        {
            if (audioClip.frequency != sampleRate)
            {
                int newSampleCount = Mathf.FloorToInt((float)audioClip.samples * sampleRate / audioClip.frequency);
                AudioClip newClip = AudioClip.Create(audioClip.name, newSampleCount, audioClip.channels, sampleRate, false);

                float[] samples = new float[audioClip.samples * audioClip.channels];
                audioClip.GetData(samples, 0);

                float[] newSamples = new float[newSampleCount * audioClip.channels];
                for (int i = 0; i < newSampleCount; i++)
                {
                    int oldIndex = Mathf.FloorToInt((float)i * audioClip.samples / newSampleCount);
                    for (int j = 0; j < audioClip.channels; j++)
                    {
                        newSamples[i * audioClip.channels + j] = samples[oldIndex * audioClip.channels + j];
                    }
                }

                newClip.SetData(newSamples, 0);
                audioClip = newClip;
            }
            return audioClip;
        }

        /// <summary>
        /// Stops recording audio
        /// </summary>
        public void StopRecording()
        {
            if (!IsRecording) return;
            IsRecording = false;

            switch (_recordingMode)
            {
                case RecordingMode.Microphone:
                    Microphone.End(CurrentDeviceName);
                    Destroy(_audioClip);
                    _audioClip = null;
                    _didDetect = false;

                    if (_recordCoroutine != null) StopCoroutine(RecordMicrophoneData());
                    break;
                case RecordingMode.AudioFile:
                    Microphone.End(null);
                    _audioClip = null;
                    _didDetect = false;

                    if (_recordCoroutine != null) StopCoroutine(RecordAudioFileData());
                    break;
            }
        }

        /// <summary>
        /// Loop for buffering incoming audio data and delivering frames of microphone data
        /// </summary>
        private IEnumerator RecordMicrophoneData()
        {
            float[] sampleBuffer = new float[FrameLength];
            int startReadPos = 0;

            OnRecordingStart?.Invoke();
            //Debug.Log("Recording started");

            while (IsRecording)
            {
                int curClipPos = Microphone.GetPosition(CurrentDeviceName);
                if (curClipPos < startReadPos)
                    curClipPos += _audioClip.samples;

                int samplesAvailable = curClipPos - startReadPos;
                //Debug.Log($"Current clip position: {curClipPos}, Start read position: {startReadPos}, Samples available: {samplesAvailable}");

                if (samplesAvailable < FrameLength)
                {
                    yield return null;
                    continue;
                }

                int endReadPos = startReadPos + FrameLength;
                if (endReadPos > _audioClip.samples)
                {
                    // fragmented read (wraps around to beginning of clip)
                    // read bit at end of clip
                    int numSamplesClipEnd = _audioClip.samples - startReadPos;
                    float[] endClipSamples = new float[numSamplesClipEnd];
                    _audioClip.GetData(endClipSamples, startReadPos);

                    // read bit at start of clip
                    int numSamplesClipStart = endReadPos - _audioClip.samples;
                    float[] startClipSamples = new float[numSamplesClipStart];
                    _audioClip.GetData(startClipSamples, 0);

                    // combine to form full frame
                    Buffer.BlockCopy(endClipSamples, 0, sampleBuffer, 0, numSamplesClipEnd);
                    Buffer.BlockCopy(startClipSamples, 0, sampleBuffer, numSamplesClipEnd, numSamplesClipStart);

                    //Debug.Log($"Fragmented read: numSamplesClipEnd: {numSamplesClipEnd}, numSamplesClipStart: {numSamplesClipStart}");
                }
                else
                {
                    _audioClip.GetData(sampleBuffer, startReadPos);
                    //Debug.Log($"Normal read: startReadPos: {startReadPos}, endReadPos: {endReadPos}");
                }
                startReadPos = endReadPos % _audioClip.samples;

                //Debug.Log($"Processing buffer, first 10 samples: {string.Join(", ", sampleBuffer.Take(10))}");
                ProcessAudioBuffer(sampleBuffer);
            }

            OnRecordingStop?.Invoke();
            RestartRecording?.Invoke();
            //Debug.Log("Recording stopped");
        }

        /// <summary>
        /// Loop for buffering incoming audio data and delivering frames of audio file data
        /// </summary>
        private IEnumerator RecordAudioFileData()
        {
            float[] sampleBuffer = new float[FrameLength];
            int startReadPos = 0;
            float clipLength = _audioClip.length;
            float startTime = Time.time;

            OnRecordingStart?.Invoke();
            //Debug.Log("Recording started");
            PlayAudioFile();

            while (IsRecording)
            {
                float elapsedTime = Time.time - startTime;
                int curClipPos = Mathf.FloorToInt(elapsedTime / clipLength * _audioClip.samples);
                int samplesAvailable = curClipPos - startReadPos;
                //Debug.Log($"Elapsed time: {elapsedTime}, Current clip position: {curClipPos}, Start read position: {startReadPos}, Samples available: {samplesAvailable}");

                if (samplesAvailable < FrameLength)
                {
                    yield return null;
                    continue;
                }

                int endReadPos = startReadPos + FrameLength;
                if (endReadPos > _audioClip.samples)
                {
                    // fragmented read (wraps around to beginning of clip)
                    // read bit at end of clip
                    int numSamplesClipEnd = _audioClip.samples - startReadPos;
                    float[] endClipSamples = new float[numSamplesClipEnd];
                    _audioClip.GetData(endClipSamples, startReadPos);

                    // read bit at start of clip
                    int numSamplesClipStart = endReadPos - _audioClip.samples;
                    float[] startClipSamples = new float[numSamplesClipStart];
                    _audioClip.GetData(startClipSamples, 0);

                    // combine to form full frame
                    Buffer.BlockCopy(endClipSamples, 0, sampleBuffer, 0, numSamplesClipEnd);
                    Buffer.BlockCopy(startClipSamples, 0, sampleBuffer, numSamplesClipEnd, numSamplesClipStart);

                    //Debug.Log($"Fragmented read: numSamplesClipEnd: {numSamplesClipEnd}, numSamplesClipStart: {numSamplesClipStart}");
                }
                else
                {
                    _audioClip.GetData(sampleBuffer, startReadPos);
                    //Debug.Log($"Normal read: startReadPos: {startReadPos}, endReadPos: {endReadPos}");
                }
                startReadPos = endReadPos % _audioClip.samples;

                //Debug.Log($"Processing buffer, first 10 samples: {string.Join(", ", sampleBuffer.Take(10))}");
                ProcessAudioBuffer(sampleBuffer);

                // Check if the end of the clip has been reached
                if (curClipPos >= _audioClip.samples)
                {
                    //Debug.Log("End of audio clip reached");
                    break;
                }
            }

            StopRecording();
            OnRecordingStop?.Invoke();
            RestartRecording?.Invoke();
            //Debug.Log("Recording stopped");
        }

        /// <summary>
        /// Process audio buffer and raise event for frame captured
        /// </summary>
        /// <param name="buffer">Audio buffer to process</param>
        private void ProcessAudioBuffer(float[] buffer)
        {
            if (_autoDetect == false)
            {
                _transmit = _audioDetected = true;
            }
            else
            {
                float maxVolume = 0.0f;

                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i] > maxVolume)
                    {
                        maxVolume = buffer[i];
                    }
                }

                if (maxVolume >= _minimumSpeakingSampleValue)
                {
                    _transmit = _audioDetected = true;
                    _timeAtSilenceBegan = Time.time;
                }
                else
                {
                    _transmit = false;

                    if (_audioDetected && Time.time - _timeAtSilenceBegan > _silenceTimer)
                    {
                        _audioDetected = false;
                    }
                }
            }

            //Debug.Log(_audioDetected + " " + _transmit);
            if (_audioDetected)
            {
                _didDetect = true;
                // converts to 16-bit int samples
                short[] pcmBuffer = new short[buffer.Length];
                for (int i = 0; i < buffer.Length; i++)
                {
                    pcmBuffer[i] = (short)Math.Floor(buffer[i] * short.MaxValue);
                }

                // raise buffer event
                if (OnFrameCaptured != null && _transmit)
                {
                    // 10 first samples of the buffer
                    //Debug.Log("First 10 samples: " + string.Join(", ", new List<short>(pcmBuffer).GetRange(0, 10).ConvertAll(i => i.ToString()).ToArray()));
                    OnFrameCaptured.Invoke(pcmBuffer);
                }
            }
            else
            {
                if (_didDetect)
                {
                    OnRecordingStop?.Invoke();
                    _didDetect = false;
                }
            }
        }

        /// <summary>
        /// Play the audio file in the scene
        /// </summary>
        public void PlayAudioFile()
        {
            if (!gameObject.TryGetComponent(out _audioSource)) _audioSource = gameObject.AddComponent<AudioSource>();

            if (_audioClip != null)
            {
                _audioSource.clip = _audioClip;
                _audioSource.Play();
                Debug.Log("Playing audio file");
            }
            else
            {
                Debug.LogError("No audio clip available to play");
            }
        }

        /// <summary>
        /// Save a WAV file to disk
        /// </summary>
        /// <param name="filePath">Path to save the WAV file </param>
        /// <param name="data">Audio data to save </param>
        private void SaveWavFile(string filePath, byte[] data)
        {
            using var fileStream = new FileStream(filePath, FileMode.Create);
            using var writer = new BinaryWriter(fileStream);
            // Write WAV header
            writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + data.Length);
            writer.Write(new char[4] { 'W', 'A', 'V', 'E' });
            writer.Write(new char[4] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(16000);
            writer.Write(16000 * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(new char[4] { 'd', 'a', 't', 'a' });
            writer.Write(data.Length);
            writer.Write(data);

            Debug.Log("WAV file saved to " + filePath);
        }

        /// <summary>
        /// Load a WAV file from disk
        /// </summary>
        /// <param name="path">Path to the WAV file </param>
        /// <returns>AudioClip containing the WAV file data</returns>
        private AudioClip LoadWavFile(string path)
        {
            using var fileStream = new FileStream(path, FileMode.Open);
            using var reader = new BinaryReader(fileStream);

            // Read WAV header
            char[] riff = reader.ReadChars(4);
            int chunkSize = reader.ReadInt32();
            char[] wave = reader.ReadChars(4);
            char[] fmt = reader.ReadChars(4);
            int subchunk1Size = reader.ReadInt32();
            int audioFormat = reader.ReadInt16();
            int numChannels = reader.ReadInt16();
            int sampleRate = reader.ReadInt32();
            int byteRate = reader.ReadInt32();
            int blockAlign = reader.ReadInt16();
            int bitsPerSample = reader.ReadInt16();
            char[] data = reader.ReadChars(4);
            int subchunk2Size = reader.ReadInt32();

            // Read audio data
            byte[] audioData = reader.ReadBytes(subchunk2Size);

            // Convert byte data to float samples
            float[] samples = new float[audioData.Length / 2];

            for (int i = 0; i < samples.Length; i++)
            {
                short sample = (short)((audioData[i * 2 + 1] << 8) | audioData[i * 2]);
                samples[i] = sample / (float)short.MaxValue;
            }

            AudioClip audioClip = AudioClip.Create("WavFile", samples.Length, numChannels, sampleRate, false);
            audioClip.SetData(samples, 0);

            return audioClip;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Load a WAV file from disk
        /// </summary>
        [Button("Load Audio File"), ShowIf("IsAudioFileMode")]
        public void LoadAudioFile()
        {
            string path = UnityEditor.EditorUtility.OpenFilePanel("Load Audio File", "", "wav");
            if (path.Length != 0)
            {
                _audioClip = LoadWavFile(path);
            }
            StartRecording();
        }
#endif
    }
}