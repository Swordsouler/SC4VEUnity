using System;
using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using Sven.Multimodality.Vocal;
using UnityEngine;

public enum RecordingMode
{
    Microphone,
    Environment
}

public class VoiceProcessor : MonoBehaviour
{
    #region Flags
    /// <summary>
    /// Indicates whether microphone is capturing or not
    /// </summary>
    public bool IsRecording => (_recordingMode == RecordingMode.Microphone && _audioClip != null && Microphone.IsRecording(CurrentDeviceName)) || (_recordingMode == RecordingMode.Environment && _audioCapture != null && _audioCapture.OnAudioFilterReadEvent.GetPersistentEventCount() > 0);

    public bool IsEnvironmentMode => _recordingMode == RecordingMode.Environment;
    public bool IsMicrophoneMode => _recordingMode == RecordingMode.Microphone;
    #endregion

    [SerializeField] private RecordingMode _recordingMode;
    [SerializeField, ShowIf("IsEnvironmentMode")] private AudioCapture _audioCapture;
    [SerializeField, ShowIf("IsMicrophoneMode")] private int MicrophoneIndex;

    public int SampleRate { get; private set; }
    public int FrameLength { get; private set; }

    public event Action<short[]> OnFrameCaptured;
    public event Action OnRecordingStop;
    public event Action OnRecordingStart;
    public List<string> Devices { get; private set; }
    public int CurrentDeviceIndex { get; private set; }
    public string CurrentDeviceName
    {
        get
        {
            if (CurrentDeviceIndex < 0 || CurrentDeviceIndex >= Microphone.devices.Length)
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

    private AudioClip _audioClip;
    private event Action RestartRecording;

    void Awake()
    {
        UpdateDevices();
    }

#if UNITY_EDITOR
    void Update()
    {
        if (CurrentDeviceIndex != MicrophoneIndex)
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
                SampleRate = sampleRate;
                FrameLength = frameSize;

                _audioClip = Microphone.Start(CurrentDeviceName, true, 1, sampleRate);
                StartCoroutine(RecordMicrophoneData());
                break;
            case RecordingMode.Environment:
                if (_audioCapture != null) _audioCapture.OnAudioFilterReadEvent.AddListener(ProcessAudioData);
                break;
        }
    }

    /// <summary>
    /// Stops recording audio
    /// </summary>
    public void StopRecording()
    {
        if (!IsRecording) return;

        switch (_recordingMode)
        {
            case RecordingMode.Microphone:
                Microphone.End(CurrentDeviceName);
                Destroy(_audioClip);
                _audioClip = null;
                _didDetect = false;

                StopCoroutine(RecordMicrophoneData());
                break;
            case RecordingMode.Environment:
                if (_audioCapture != null) _audioCapture.OnAudioFilterReadEvent.RemoveListener(ProcessAudioData);
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

        while (IsRecording)
        {
            int curClipPos = Microphone.GetPosition(CurrentDeviceName);
            if (curClipPos < startReadPos)
                curClipPos += _audioClip.samples;

            int samplesAvailable = curClipPos - startReadPos;
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
            }
            else
            {
                _audioClip.GetData(sampleBuffer, startReadPos);
            }
            startReadPos = endReadPos % _audioClip.samples;

            ProcessAudioBuffer(sampleBuffer);
        }

        OnRecordingStop?.Invoke();
        RestartRecording?.Invoke();
    }

    private void ProcessAudioData(float[] data, int channels)
    {
        ProcessAudioBuffer(data);
    }

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
                OnFrameCaptured.Invoke(pcmBuffer);
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
}