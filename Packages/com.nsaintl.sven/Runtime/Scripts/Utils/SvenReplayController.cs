// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Sven.GraphManagement;
using Sven.OwlTime;
using System.Globalization;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sven.Utils
{
    /// <summary>
    /// Controller to manage the SvenReader.
    /// </summary>
    public class SvenReplayController : MonoBehaviour
    {
        /// <summary>
        /// Slider to control the time.
        /// </summary>
        [SerializeField]
        private Slider _timeSlider;

        /// <summary>
        /// Text to show the time.
        /// </summary>
        [SerializeField]
        private TextMeshProUGUI _timeText;

        /// <summary>
        /// Toggle to play or pause the time.
        /// </summary>
        [SerializeField]
        private Toggle _playPauseButton;

        /// <summary>
        /// Sprites for the play and pause buttons.
        /// </summary>
        [SerializeField]
        private Sprite _playSprite;

        /// <summary>
        /// Sprites for the play and pause buttons.
        /// </summary>
        [SerializeField]
        private Sprite _pauseSprite;

        /// <summary>
        /// Button to step backward in time.
        /// </summary>
        [SerializeField]
        private Button _backwardButton;

        /// <summary>
        /// Button to step forward in time.
        /// </summary>
        [SerializeField]
        private Button _forwardButton;

        /// <summary>
        /// Dropdown to select the speed of the time.
        /// </summary>
        [SerializeField]
        private TMP_Dropdown _speedDropdown;

        /// <summary>
        /// Flag to check if the time is playing.
        /// </summary>
        private bool _isPlaying;
        /// <summary>
        /// Flag to check if the time is playing.
        /// </summary>
        private bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                _isPlaying = value;
                // get first component in childs with image and change sprite
                _playPauseButton.GetComponentInChildren<Image>().sprite = _isPlaying ? _pauseSprite : _playSprite;
            }
        }

        private bool _isRunningQuery = false;
        private float? _pendingSceneValue = null;

        private void Start()
        {
            IsPlaying = false;
            Load();
        }

        private async void Load()
        {
            await GraphManager.Reload();
            await GraphManager.LoadInstantsFromEndpoint();
            ResetController();
            OnSliderValueChanged(0);
        }

        private void OnSliderValueChanged(float value)
        {
            _ = RetrieveScene(value);
            int hours = (int)value / 3600;
            int minutes = (int)value % 3600 / 60;
            float seconds = value % 60;

            if (hours > 0)
                _timeText.text = string.Format(CultureInfo.InvariantCulture, "{0}:{1:D2}:{2:D2}", hours, minutes, (int)seconds);
            else
                _timeText.text = string.Format(CultureInfo.InvariantCulture, "{0}:{1:D2}", minutes, (int)seconds);
        }

        private async Task RetrieveScene(float value)
        {
            if (_isRunningQuery)
            {
                _pendingSceneValue = value; // Mémorise la dernière valeur demandée
                return;
            }

            _isRunningQuery = true;
            try
            {
                await GraphManager.RetrieveSceneFromEndpoint(GraphManager.SearchInstant(value));
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error retrieving scene: " + e.Message);
            }
            finally
            {
                _isRunningQuery = false;

                // Si une nouvelle valeur a été demandée pendant le traitement, on la traite maintenant
                if (_pendingSceneValue.HasValue)
                {
                    float nextValue = _pendingSceneValue.Value;
                    _pendingSceneValue = null;
                    await RetrieveScene(nextValue);
                }
            }
        }

        /// <summary>
        /// Reset the controller to its default state.
        /// </summary>
        private void ResetController()
        {
            _timeSlider.minValue = 0;
            _timeSlider.maxValue = GraphManager.Duration;
            _timeSlider.value = 0;
            _timeSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
            _timeSlider.onValueChanged.AddListener(OnSliderValueChanged);
            _playPauseButton.onValueChanged.RemoveListener(OnPlayValueChanged);
            _playPauseButton.onValueChanged.AddListener(OnPlayValueChanged);
            _backwardButton.onClick.RemoveListener(StepBackward);
            _backwardButton.onClick.AddListener(StepBackward);
            _forwardButton.onClick.RemoveListener(StepForward);
            _forwardButton.onClick.AddListener(StepForward);
        }

        private void OnPlayValueChanged(bool value)
        {
            IsPlaying = !IsPlaying;
        }

        /// <summary>
        /// Removes the UI listeners registered in ResetController so they don't leak past this component's lifetime.
        /// </summary>
        private void OnDestroy()
        {
            if (_timeSlider != null) _timeSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
            if (_playPauseButton != null) _playPauseButton.onValueChanged.RemoveListener(OnPlayValueChanged);
            if (_backwardButton != null) _backwardButton.onClick.RemoveListener(StepBackward);
            if (_forwardButton != null) _forwardButton.onClick.RemoveListener(StepForward);
        }

        private void Update()
        {
            float speed = _speedDropdown.value switch
            {
                0 => 8,
                1 => 4,
                2 => 2,
                3 => 1,
                4 => 0.75f,
                5 => 0.5f,
                6 => 0.25f,
                _ => 1
            };
            if (IsPlaying)
            {
                _timeSlider.value += Time.deltaTime * speed;
                if (_timeSlider.value >= _timeSlider.maxValue)
                {
                    IsPlaying = false;
                }
            }

            // keyboard shortcuts
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.spaceKey.wasPressedThisFrame) IsPlaying = !IsPlaying;
            if (keyboard.leftArrowKey.wasPressedThisFrame) StepBackward();
            if (keyboard.rightArrowKey.wasPressedThisFrame) StepForward();
            if (keyboard.numpadPlusKey.wasPressedThisFrame) _speedDropdown.value = Mathf.Clamp(_speedDropdown.value - 1, 0, _speedDropdown.options.Count - 1);
            if (keyboard.numpadMinusKey.wasPressedThisFrame) _speedDropdown.value = Mathf.Clamp(_speedDropdown.value + 1, 0, _speedDropdown.options.Count - 1);
        }

        /// <summary>
        /// Step forward in time.
        /// </summary>
        private void StepForward()
        {
            Instant instant = GraphManager.NextInstant();
            if (instant != null) _timeSlider.value = (float)(instant.inXSDDateTime - GraphManager.StartedAt).TotalSeconds;
        }

        /// <summary>
        /// Step backward in time.
        /// </summary>
        private void StepBackward()
        {
            Instant instant = GraphManager.PreviousInstant();
            if (instant != null) _timeSlider.value = (float)(instant.inXSDDateTime - GraphManager.StartedAt).TotalSeconds;
        }
    }
}