// Copyright (c) 2025 CNRS, LISN – Université Paris-Saclay
// Author: Nicolas SAINT-LÉGER
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using NaughtyAttributes;
using Sven.GraphManagement;
using Sven.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VDS.RDF;
using VDS.RDF.Query;

namespace Sven.Demo
{
    [Serializable]
    public class DemoMainMenuPage
    {
        public string title;
        public GameObject form;
        public List<Button> buttons = new();
    }

    public class DemoMainMenuController : MonoBehaviour
    {
        [BoxGroup("Model")] public List<DemoMainMenuPage> pages = new();

        [BoxGroup("View")] public TMP_InputField VENameInputField;
        [BoxGroup("View")] public TMP_Dropdown VENameDropdown;
        [BoxGroup("View")] public Button playButton, replayButton;
        [BoxGroup("View")] public TextMeshProUGUI subTitleText;
        [BoxGroup("View")] public GameObject dropdownActivityIndicator;
        [BoxGroup("View")] public Slider semantisationFrequencySlider;

        private DemoMainMenuPage _currentPage = null;
        private readonly Dictionary<string, string> _dropdownOptions = new();

        private void Start()
        {
            if (semantisationFrequencySlider != null) semantisationFrequencySlider.value = SvenSettings.SemanticizeFrequency; //DemoGraphConfig.semantisationFrequency;
            InitializeButtons();
            InitializeDropdownAsync();
            // load main menu form
            SetPage(pages[0]);
        }

        private void InitializeButtons()
        {
            foreach (DemoMainMenuPage menuPage in pages)
                foreach (Button button in menuPage.buttons)
                    if (button != null) button.onClick.AddListener(() => SetPage(menuPage));
            if (playButton != null) playButton.onClick.AddListener(OnPlayButtonClicked);
            if (replayButton != null) replayButton.onClick.AddListener(OnReplayButtonClicked);
        }

        public void RefreshExistentVEs()
        {
            InitializeDropdownAsync();
        }

        private async void InitializeDropdownAsync()
        {
            SetDropdownLoadingState(true);

            await LoadExistentVEs();

            SetDropdownLoadingState(false);
            UpdateDropdownSelection();
        }

        private void SetDropdownLoadingState(bool isLoading)
        {
            dropdownActivityIndicator.SetActive(isLoading);
            if (isLoading) VENameDropdown.ClearOptions();
        }

        private void UpdateDropdownSelection()
        {
            if (VENameDropdown.options.Count > 0)
            {
                string graphName = SvenSettings.GraphName;
                int index = VENameDropdown.options.FindIndex(option => _dropdownOptions.TryGetValue(option.text, out string name) && name == graphName);
                VENameDropdown.value = index != -1 ? index : 0;
                VENameDropdown.RefreshShownValue();
            }
        }

        private void SetPage(DemoMainMenuPage page)
        {
            if (_currentPage == page) return;
            _currentPage = page;
            foreach (DemoMainMenuPage menuPage in pages)
            {
                if (menuPage.form != null) menuPage.form.SetActive(menuPage == page);
                foreach (Button button in menuPage.buttons)
                    if (button != null) button.interactable = menuPage != page;
            }
            if (subTitleText != null) subTitleText.text = page.title;
        }

        private async void OnReplayButtonClicked()
        {
            if (_dropdownOptions.Count < 1 || !_dropdownOptions.TryGetValue(VENameDropdown.options[VENameDropdown.value].text, out string graphName)) return;
            SvenSettings.GraphName = string.IsNullOrEmpty(graphName) ? "default" : graphName;
            await GraphManager.Reload();
            SceneManager.LoadScene("Demo Replay", LoadSceneMode.Single);
        }

        private async void OnPlayButtonClicked()
        {
            SvenSettings.SemanticizeFrequency = (int)semantisationFrequencySlider.value;
            SvenSettings.GraphName = string.IsNullOrEmpty(VENameInputField.text) ? "default" : VENameInputField.text;
            await GraphManager.Reload();
            SceneManager.LoadScene("Demo Record", LoadSceneMode.Single);
        }
        private async Task<string> LoadQueryFileAsync(string relativePath)
        {
            string queryFilePath = System.IO.Path.Combine(Application.streamingAssetsPath, relativePath);

            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                UnityWebRequest request = UnityWebRequest.Get(queryFilePath);
                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    return request.downloadHandler.text;
                }
                else
                {
                    Debug.LogError($"Erreur lors du chargement du fichier SPARQL : {request.error}");
                    return null;
                }
            }
            else
            {
                return System.IO.File.ReadAllText(queryFilePath);
            }
        }

        private async Task LoadExistentVEs()
        {
            try
            {
                string query = @"PREFIX time: <http://www.w3.org/2006/time#>

SELECT DISTINCT ?graphName ?minInstant ?maxInstant (?maxInstant - ?minInstant AS ?duration)
WHERE {
    SELECT DISTINCT ?graphName (MIN(?instantDateTime) AS ?minInstant)  (MAX(?instantDateTime) AS ?maxInstant)
    WHERE {
        GRAPH ?graphName {
            ?instant a time:Instant ;
                     time:inXSDDateTime ?instantDateTime .
        }
    } GROUP BY ?graphName ORDER BY ?graphName LIMIT 30
} ORDER BY DESC(?minInstant) LIMIT 30"; //await LoadQueryFileAsync("SPARQL/ListExistentVEs.sparql");

                SparqlResultSet results = await GraphManager.QueryEndpoint(SvenSettings.EndpointUrl, query);

                VENameDropdown.options.Clear();
                _dropdownOptions.Clear();
                foreach (SparqlResult result in results.Cast<SparqlResult>())
                {
                    string optionContent = "";

                    if (result["minInstant"] is not ILiteralNode instantNode) continue;
                    if (result["graphName"] is not IUriNode graphNameNode) continue;
                    if (result["duration"] is not ILiteralNode durationNode) continue;

                    string graphName = graphNameNode.Uri.ToString().Split("/")[^2];

                    optionContent += $"[{FormatDate(instantNode.Value)}]";
                    optionContent += $" <b>{graphName}</b>";
                    optionContent += $" | {FormatDuration(durationNode.Value)}";

                    VENameDropdown.options.Add(new TMP_Dropdown.OptionData(optionContent));
                    _dropdownOptions.Add(optionContent, graphName);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading graph names: {e.Message}");
            }
        }

        // 2025-04-07T16:13:07.300+02:00 -> 07/04/2025 16:13:07 depending of the right utc offset
        private string FormatDate(string value)
        {
            try
            {
                DateTimeOffset dateTimeOffset = DateTimeOffset.Parse(value);
                DateTime localDateTime = dateTimeOffset.LocalDateTime;
                return localDateTime.ToString("dd/MM/yyyy HH:mm");
            }
            catch (FormatException)
            {
                Debug.LogError($"Format de date invalide : {value}");
                return value;
            }
        }

        // P0Y0M0DT1H1M11.900S -> 01h01m11s
        private string FormatDuration(string rawDuration)
        {
            if (rawDuration.Length < 10) return rawDuration;
            string[] parts = rawDuration.Split(new[] { 'P', 'Y', 'M', 'D', 'T', 'H', 'S' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return rawDuration;

            string hours = parts[^3].PadLeft(2, '0');
            string minutes = parts[^2].PadLeft(2, '0');
            string seconds = parts[^1].Split(".")[0].PadLeft(2, '0');

            return $"{(hours != "00" ? hours + "h" : "")}{(hours != "00" || minutes != "00" ? minutes + "m" : "")}{seconds}s";
        }
    }
}
