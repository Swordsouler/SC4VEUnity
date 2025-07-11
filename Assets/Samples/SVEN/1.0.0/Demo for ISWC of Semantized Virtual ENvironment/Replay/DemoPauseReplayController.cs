// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NaughtyAttributes;
using Sven.GraphManagement;
using Sven.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using VDS.RDF;
using System.Text;
using VDS.RDF.Writing;
using System;
using VDS.RDF.Parsing;
#endif

namespace Sven.Demo
{
    public class DemoPauseReplayController : DemoPauseController
    {
        [BoxGroup("View")] public Button printResultButton;
        [BoxGroup("View")] public Button downloadButton;
        [BoxGroup("View")] public Button quitButton;
        [BoxGroup("View")] public GameObject downloadingActivityIndicator;

        private bool _isDownloading = false;

        public new void Awake()
        {
            base.Awake();
            InitializeButtons();
        }

        private void InitializeButtons()
        {
            if (downloadingActivityIndicator != null) downloadingActivityIndicator.SetActive(false);
            if (printResultButton != null) printResultButton.onClick.AddListener(GraphManager.PrintExperimentResults);
            if (downloadButton != null) downloadButton.onClick.AddListener(OnDownloadButtonClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitButtonClicked);
        }

        public async void OnDownloadButtonClicked()
        {
            if (_isDownloading) return;
            _isDownloading = true;
            if (downloadingActivityIndicator != null) downloadingActivityIndicator.SetActive(true);
            if (downloadButton != null) downloadButton.gameObject.SetActive(false);
            string turtleContent = await GraphManager.DownloadTTLFromEndpoint(SvenSettings.EndpointUrl);

            // in webgl build, download the turtleContent ass txt file
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

        private void OnQuitButtonClicked()
        {
            if (_isDownloading) return;
            SceneManager.LoadScene("Demo Menu", LoadSceneMode.Single);
        }

        public new void Update()
        {
            if (_isDownloading) return;
            base.Update();
        }

        public new void TogglePause()
        {
            if (_isDownloading) return;
            base.TogglePause();
        }
    }
}