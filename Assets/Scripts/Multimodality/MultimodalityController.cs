using Newtonsoft.Json;
using Sven.Command;
using Sven.Content;
using Sven.GraphManagement;
using Sven.Multimodality.Voice;
using Sven.OwlTime;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Sven.Multimodality
{
    public class MultimodalityController : MonoBehaviour
    {
        private static MultimodalityController _instance;

        private static List<SemantizationCore> _selectedObjects = new();
        public static IReadOnlyList<SemantizationCore> SelectedObjects => _selectedObjects;

        public VoskSpeechToText VoskSpeechToText;
        [SerializeField] private CommandExecutionMode _commandExecutionMode = CommandExecutionMode.Algorithm;

        private static readonly ConcurrentQueue<Action> _mainThreadActions = new();

        private void OnTranscriptionResult(string obj)
        {
            var result = new RecognitionResult(obj);
            for (int i = 0; i < result.Phrases.Length; i++)
            {
                if (result.Phrases[i].Text == "") continue;

                _commandChain = new CommandChain(_commandExecutionMode, result.Phrases[i], Settings);
                _commandChain.Execute();
            }
        }

        public static void AddSelectedObject(SemantizationCore semantizationCore)
        {
            if (!_selectedObjects.Contains(semantizationCore))
            {
                _selectedObjects.Add(semantizationCore);
                SetHighlight(semantizationCore, true);
            }
        }

        public static void AddSelectedObjects(IEnumerable<SemantizationCore> semantizationCores, bool intersection)
        {
            var semantizationCoreList = semantizationCores.ToList();
            if (_selectedObjects.Count != 0 && intersection)
            {
                // Call removeSelectedObjects to remove objects not in the new selection
                var toRemove = _selectedObjects.Except(semantizationCoreList).ToList();
                Debug.Log($"[MultimodalityController] Number of items to remove: {toRemove.Count}");
                foreach (var semantizationCore in toRemove)
                {
                    RemoveSelectedObject(semantizationCore);
                }
            }
            else
            {
                Debug.Log($"[MultimodalityController] Number of items to add: {semantizationCoreList.Count}");
                foreach (SemantizationCore semantizationCore in semantizationCoreList)
                {
                    if (semantizationCore == null)
                    {
                        Debug.LogWarning("[MultimodalityController] A null SemantizationCore was found in the list and will be skipped.");
                        continue;
                    }
                    AddSelectedObject(semantizationCore);
                }
            }
        }

        public static void RemoveSelectedObject(SemantizationCore semantizationCore)
        {
            if (_selectedObjects.Contains(semantizationCore))
            {
                _selectedObjects.Remove(semantizationCore);
                SetHighlight(semantizationCore, false);
            }
        }

        public static void RemoveSelectedObjects(IReadOnlyList<SemantizationCore> semantizationCores)
        {
            Debug.Log($"[MultimodalityController] Number of items to remove: {semantizationCores.Count}");
            foreach (var semantizationCore in semantizationCores)
            {
                RemoveSelectedObject(semantizationCore);
            }
        }

        public static void ClearSelectedObjects()
        {
            Debug.Log($"[MultimodalityController] Number of items to clear: {_selectedObjects.Count}");
            foreach (var obj in _selectedObjects)
            {
                SetHighlight(obj, false);
            }
            _selectedObjects.Clear();
        }

        private static void SetHighlight(SemantizationCore semantizationCore, bool highlight)
        {
            if (_instance != null)
            {
                // Place l'action dans la file d'attente pour exécution sur le thread principal
                _mainThreadActions.Enqueue(() => _instance.StartCoroutine(_instance.SetHighlightCoroutine(semantizationCore, highlight)));
            }
        }

        public IEnumerator SetHighlightCoroutine(SemantizationCore semantizationCore, bool highlight)
        {
            yield return null;
            if (highlight)
            {
                if (semantizationCore.TryGetComponent(out Outline outline))
                    outline.enabled = true;
                else
                    semantizationCore.gameObject.AddComponent<Outline>();
            }
            else
            {
                if (semantizationCore.TryGetComponent(out Outline outline))
                    outline.enabled = false;
            }
        }

        void Update()
        {
            while (_mainThreadActions.TryDequeue(out var action))
            {
                action?.Invoke();
            }

            if (Input.GetKeyDown(KeyCode.T))
                ExampleTest();
            if (Input.GetKeyDown(KeyCode.Y))
                Y();
            if (Input.GetKeyDown(KeyCode.Alpha1))
                Example1Word();
            if (Input.GetKeyDown(KeyCode.Alpha2))
                Example2Word();
            if (Input.GetKeyDown(KeyCode.Alpha3))
                Example3Word();
            /*if (Input.GetKeyDown(KeyCode.Space))
            {
                // Retrieve all SemantizationCore objects in the scene
                var semantizationCores = new List<SemantizationCore>(FindObjectsByType<SemantizationCore>(FindObjectsSortMode.None));
                AddSelectedObjects(semantizationCores, false);
            }

            if (Input.GetKeyDown(KeyCode.LeftAlt))
            {
                // Retrieve all SemantizationCore objects in the scene
                var semantizationCores = new List<SemantizationCore>(FindObjectsByType<SemantizationCore>(FindObjectsSortMode.None));
                RemoveSelectedObjects(semantizationCores);
            }*/
        }

        private async void Rollback(float time)
        {
            // Calculate the new time
            DateTime targetDateTime = DateTime.Now.AddSeconds(-time);
            Instant targetInstant = GraphManager.SearchInstant(targetDateTime);
            Debug.Log($"Rolling back to {targetDateTime} ({targetInstant})");
            await GraphManager.SaveToEndpoint();
            await GraphManager.RetrieveSceneFromEndpoint(targetInstant);
            //await GraphManager.RetrieveSceneFromMemory(targetInstant);
        }

        private CommandChain _commandChain;

        private void T()
        {
            _commandChain = new CommandChain();
            _commandChain.AddCommand(new SelectCommand { Parameter = new PointOfViewFilter(DateTime.Now) }, "Manual Test T");
            _commandChain.AddCommand(new HideCommand(), "Manual Test T");
            _commandChain.AddCommand(new UnselectCommand { Parameter = new AllFilter(DateTime.Now) }, "Manual Test T");
            _commandChain.Execute();
        }

        private void Y()
        {
            _commandChain = new CommandChain();
            _commandChain.AddCommand(new SelectCommand { Parameter = new AnnotationFilter("sven:Pumpkin", DateTime.Now) }, "Manual Test Y");
            _commandChain.AddCommand(new ShowCommand(), "Manual Test Y");
            _commandChain.AddCommand(new UnselectCommand { Parameter = new AllFilter(DateTime.Now) }, "Manual Test Y");
            _commandChain.Execute();
        }

        private void Example1()
        {
            _commandChain = new CommandChain();
            _commandChain.AddCommand(new SelectCommand { Parameter = new AnnotationFilter("sven:Apple", DateTime.Now) }, "Manual Test Example1");
            _commandChain.AddCommand(new ColorizeCommand { Parameter = new ColorParameter { Red = 1f, Green = 0f, Blue = 0f } }, "Manual Test Example1");
            _commandChain.AddCommand(new UnselectCommand { Parameter = new AllFilter(DateTime.Now) }, "Manual Test Example1");
            _commandChain.Execute();
        }

        private void Example2()
        {
            _commandChain = new CommandChain();
            _commandChain.AddCommand(new SelectCommand { Parameter = new PointOfViewFilter(DateTime.Now) }, "Manual Test Example2");
            _commandChain.AddCommand(new ColorizeCommand { Parameter = new ColorParameter { Red = 0f, Green = 1f, Blue = 0f } }, "Manual Test Example2");
            _commandChain.AddCommand(new UnselectCommand { Parameter = new AllFilter(DateTime.Now) }, "Manual Test Example2");
            _commandChain.Execute();
        }

        private void Example3()
        {
            _commandChain = new CommandChain();
            _commandChain.AddCommand(new SelectCommand { Parameter = new AnnotationFilter("sven:Pumpkin", DateTime.Now) }, "Manual Test Example3");
            _commandChain.AddCommand(new SelectCommand { Parameter = new ColorFilter(new ColorParameter { Red = 0.2f, Green = 0.8f, Blue = 0.2f, Tolerance = 0.2f }, DateTime.Now) }, "Manual Test Example3");
            _commandChain.AddCommand(new ColorizeCommand { Parameter = new ColorParameter { Red = 0f, Green = 0f, Blue = 1f } }, "Manual Test Example3");
            _commandChain.AddCommand(new UnselectCommand { Parameter = new AllFilter(DateTime.Now) }, "Manual Test Example3");
            _commandChain.Execute();
        }

        private void Example1Word()
        {
            Command.Sentence sentence = new("colorie les pommes en rouge", new List<Word>
                {
                    new("colorie", DateTime.Now.AddSeconds(-5), DateTime.Now.AddSeconds(-4)),
                    new("les", DateTime.Now.AddSeconds(-4), DateTime.Now.AddSeconds(-3)),
                    new("pommes", DateTime.Now.AddSeconds(-3), DateTime.Now.AddSeconds(-2)),
                    new("en", DateTime.Now.AddSeconds(-2), DateTime.Now.AddSeconds(-1)),
                    new("rouge", DateTime.Now.AddSeconds(-1), DateTime.Now)
                });
            _commandChain = new CommandChain(_commandExecutionMode, sentence, Settings);
            _commandChain.Execute();
        }

        private void Example2Word()
        {
            Command.Sentence sentence = new("colorie ce que je vois en vert", new List<Word>
                {
                    new("colorie", DateTime.Now.AddSeconds(-7), DateTime.Now.AddSeconds(-6)),
                    new("ce", DateTime.Now.AddSeconds(-6), DateTime.Now.AddSeconds(-5)),
                    new("que", DateTime.Now.AddSeconds(-5), DateTime.Now.AddSeconds(-4)),
                    new("je", DateTime.Now.AddSeconds(-4), DateTime.Now.AddSeconds(-3)),
                    new("vois", DateTime.Now.AddSeconds(-3), DateTime.Now.AddSeconds(-2)),
                    new("en", DateTime.Now.AddSeconds(-2), DateTime.Now.AddSeconds(-1)),
                    new("vert", DateTime.Now.AddSeconds(-1), DateTime.Now)
                });
            _commandChain = new CommandChain(_commandExecutionMode, sentence, Settings);
            _commandChain.Execute();
        }

        private void Example3Word()
        {
            Command.Sentence sentence = new("colorie les citrouilles bleu en orange", new List<Word>
                {
                    new("colorie", DateTime.Now.AddSeconds(-6), DateTime.Now.AddSeconds(-5)),
                    new("les", DateTime.Now.AddSeconds(-5), DateTime.Now.AddSeconds(-4)),
                    new("citrouilles", DateTime.Now.AddSeconds(-4), DateTime.Now.AddSeconds(-3)),
                    new("verte", DateTime.Now.AddSeconds(-3), DateTime.Now.AddSeconds(-2)),
                    new("en", DateTime.Now.AddSeconds(-2), DateTime.Now.AddSeconds(-1)),
                    new("bleu", DateTime.Now.AddSeconds(-1), DateTime.Now)
                });
            _commandChain = new CommandChain(_commandExecutionMode, sentence, Settings);
            _commandChain.Execute();
        }

        private void ExampleTest()
        {
            Sentence sentence = new("colorie en rouge les citrouille que je vois, puis cache les");
            _commandChain = new CommandChain(_commandExecutionMode, sentence, Settings);
            _commandChain.Execute();
        }

        private static Dictionary<string, BaseSettingsGUI> _settings;
        public static Dictionary<string, BaseSettingsGUI> Settings => _settings;

        private void Awake()
        {
            _instance = this;
            if (_settings == null)
            {
                // Build the absolute path to the settings file in StreamingAssets
                string settingsPath = Path.Combine(Application.streamingAssetsPath, "Multimodality", "command_settings.json");
                if (!File.Exists(settingsPath))
                {
                    Debug.LogError($"[MultimodalityController] Command settings file not found at: {settingsPath}");
                    _settings = new Dictionary<string, BaseSettingsGUI>();
                    return;
                }

                string json = File.ReadAllText(settingsPath);
                _settings = JsonConvert.DeserializeObject<Dictionary<string, BaseSettingsGUI>>(
                    json,
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Auto,
#if UNITY_EDITOR
                        Converters = new List<JsonConverter> { new UnityEventConverter(), new EventParameterConverter(), new GameObjectConverter() }
#endif
                    }
                );
                Debug.Log("[MultimodalityController] CommandSettings loaded from StreamingAssets.");
            }
            VoskSpeechToText.OnTranscriptionResult += OnTranscriptionResult;
        }

        public static void EnqueueMainThreadAction(Action action)
        {
            _mainThreadActions.Enqueue(action);
        }
    }
}