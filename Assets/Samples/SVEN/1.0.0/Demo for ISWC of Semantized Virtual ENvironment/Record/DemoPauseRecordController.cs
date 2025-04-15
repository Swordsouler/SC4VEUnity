using System;
using System.Text;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Writing;

namespace Sven.Demo
{
    public class DemoPauseRecordController : DemoPauseController
    {
        [BoxGroup("View")] public TextMeshProUGUI triplesAmountText;
        [BoxGroup("View")] public Slider sensitivitySlider;
        [BoxGroup("View")] public Button downloadButton;
        [BoxGroup("View")] public Button saveQuitButton;
        [BoxGroup("View")] public GameObject sendingActivityIndicator;

        [BoxGroup("Controller")] public DemoGraphBuffer graphBuffer;
        [BoxGroup("Controller")] public DemoCharacterController demoCharacterController;

        private bool _isSending = false;

        public new void Awake()
        {
            base.Awake();
            InitializeButtons();
        }

        public void Start()
        {
            if (sendingActivityIndicator != null) sendingActivityIndicator.SetActive(_isSending);
            if (triplesAmountText != null) triplesAmountText.text = graphBuffer.Graph.Triples.Count.ToString();
        }

        public override void TogglePause()
        {
            if (_isSending) return;
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

        private void OnDownloadButtonClicked()
        {
            string turtleContent = graphBuffer.DecodeGraph(graphBuffer.Graph);

            // in webgl build, download the turtleContent ass txt file
#if UNITY_WEBGL && !UNITY_EDITOR
            string fileName = $"sven-{DemoManager.graphName}.ttl";
            string jsCode = $@"
                var blob = new Blob([`{turtleContent}`], {{ type: 'text/plain' }});
                var link = document.createElement('a');
                link.href = URL.createObjectURL(blob);
                link.download = '{fileName}';
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
            ";
            Application.ExternalEval(jsCode);
#else
            Debug.Log("Graph content:\n" + turtleContent);
#endif
        }

        /// <summary>
        /// Decode the graph to a turtle string.
        /// </summary>
        /// <param name="graph">The graph to decode.</param>
        /// <returns>Decoded graph in turtle format.</returns>
        protected string DecodeGraph(IGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph) + " is null.");
            StringBuilder sb = new();
            CompressingTurtleWriter writer = new(TurtleSyntax.Rdf11Star);
            writer.Save(graph, new System.IO.StringWriter(sb));
            return sb.ToString();
        }

        private void OnSensitivitySliderValueChanged(float arg0)
        {
            if (demoCharacterController != null) demoCharacterController.mouseSensitivity = arg0;
        }

        private async void OnSaveQuitButtonClicked()
        {
            if (_isSending) return;
            _isSending = true;
            if (saveQuitButton != null) saveQuitButton.gameObject.SetActive(false);
            if (sendingActivityIndicator != null) sendingActivityIndicator.SetActive(true);

            await graphBuffer.SaveExperiment();
            SceneManager.LoadScene("Demo Menu", LoadSceneMode.Single);
        }

        public new void Update()
        {
            if (_isSending) return;
            base.Update();
            triplesAmountText.text = graphBuffer.Graph.Triples.Count.ToString();
        }
    }
}