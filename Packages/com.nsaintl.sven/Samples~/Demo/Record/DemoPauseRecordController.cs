// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NaughtyAttributes;
using Sven.GraphManagement;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Text;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Writing;
#endif

namespace Sven.Demo
{
    public class DemoPauseRecordController : DemoPauseController
    {
        [BoxGroup("View")] public TextMeshProUGUI triplesAmountText;
        [BoxGroup("View")] public Slider sensitivitySlider;
        [BoxGroup("View")] public Button downloadButton;
        [BoxGroup("View")] public Button saveQuitButton;
        [BoxGroup("View")] public GameObject downloadingActivityIndicator;
        [BoxGroup("View")] public GameObject sendingActivityIndicator;

        [BoxGroup("Controller")] public DemoCharacterController demoCharacterController;

        private bool _isDownloading = false;
        private bool _isSending = false;

        public new void Awake()
        {
            base.Awake();
            InitializeButtons();
        }

        public void Start()
        {
            if (downloadingActivityIndicator != null) downloadingActivityIndicator.SetActive(false);
            if (sendingActivityIndicator != null) sendingActivityIndicator.SetActive(false);
            if (triplesAmountText != null) triplesAmountText.text = GraphManager.Count.ToString();
        }

        public override void TogglePause()
        {
            if (_isSending || _isDownloading) return;
            base.TogglePause();
            if (demoCharacterController != null) demoCharacterController.enabled = !demoCharacterController.enabled;
            Cursor.lockState = demoCharacterController.enabled && demoCharacterController.lockMouse ? CursorLockMode.Locked : CursorLockMode.None;
        }

        private void InitializeButtons()
        {
            if (downloadButton != null) downloadButton.onClick.AddListener(OnDownloadButtonClicked);
            if (saveQuitButton != null) saveQuitButton.onClick.AddListener(OnSaveQuitButtonClicked);
            if (sensitivitySlider != null) sensitivitySlider.onValueChanged.AddListener(OnSensitivitySliderValueChanged);
        }

        public void OnDownloadButtonClicked()
        {
            if (_isSending || _isDownloading) return;
            _isDownloading = true;
            if (downloadingActivityIndicator != null) downloadingActivityIndicator.SetActive(true);
            if (downloadButton != null) downloadButton.gameObject.SetActive(false);
            string turtleContent = GraphManager.DecodeGraph();

            // in webgl build, download the turtleContent as txt file
#if UNITY_WEBGL && !UNITY_EDITOR
            string fileName = $"sven-{GraphManager.GraphName}.ttl";
            Application.ExternalCall("downloadFile", turtleContent, fileName);
#else
            Debug.Log("Graph content:\n" + turtleContent);
#endif
            _isDownloading = false;
            if (downloadingActivityIndicator != null) downloadingActivityIndicator.SetActive(false);
            if (downloadButton != null) downloadButton.gameObject.SetActive(true);
        }

        private void OnSensitivitySliderValueChanged(float arg0)
        {
            if (demoCharacterController != null) demoCharacterController.mouseSensitivity = arg0;
        }

        private async void OnSaveQuitButtonClicked()
        {
            if (_isSending || _isDownloading) return;
            _isSending = true;
            if (saveQuitButton != null) saveQuitButton.gameObject.SetActive(false);
            if (sendingActivityIndicator != null) sendingActivityIndicator.SetActive(true);

            //await GraphManager.ApplyRulesAsync();
            await GraphManager.AddToEndpoint();

            SceneManager.LoadScene("Demo Menu", LoadSceneMode.Single);
        }

        public new void Update()
        {
            if (_isSending || _isDownloading) return;
            base.Update();
            triplesAmountText.text = GraphManager.Count.ToString();
        }
    }
}