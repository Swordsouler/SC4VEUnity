using NaughtyAttributes;
using Newtonsoft.Json;
using Sc4ve.Multimodality.Parameter;
using Sc4ve.Voice;
using Sven.GraphManagement;
using Sven.OwlTime;
using Sven.Utils;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace Sc4ve.Multimodality
{
    public class MultimodalityController : MonoBehaviour
    {
        // faire comme le user data de spam

        [BoxGroup("References"), SerializeField] private VoskSpeechToText _voskSpeechToText;

        [BoxGroup("Settings"), SerializeField] private static Language _loadedLanguage = Language.French;
        public static Language LoadedLanguage
        {
            get => _loadedLanguage;
            set => _loadedLanguage = value;
        }
        public static string LoadedLocale => GetLocale(_loadedLanguage);
        public static string GetLocale(Language language)
        {
            return language switch
            {
                Language.French => "fr",
                Language.English => "en",
                Language.German => "de",
                Language.Italian => "it",
                Language.Russian => "ru",
                Language.Spanish => "es",
                _ => "en",
            };
        }
        private void Awake()
        {
            if (_voskSpeechToText != null) _voskSpeechToText.OnTranscriptionResult += OnTranscriptionResult;
        }

        private void OnTranscriptionResult(string obj)
        {
            var result = new RecognitionResult(obj);
            for (int i = 0; i < result.Phrases.Length; i++)
            {
                if (result.Phrases[i].Text == "") continue;

                Sentence phrase = result.Phrases[i];

                phrase.Start(new Instant(phrase.StartedAt));
                phrase.End(new Instant(phrase.EndedAt));
                phrase.Semanticize();
                /*_commandChain = new CommandChain(_commandExecutionMode, result.Phrases[i], Settings);
                _commandChain.Execute();*/
            }
        }

        private List<Command> DeserializeCommand(string json)
        {
            // using newtonsoft json
            List<Command> commands = JsonConvert.DeserializeObject<List<Command>>(json);
            return commands;
        }

        private List<Command> CommandTest1()
        {
            string jsonTest = "[\r\n  {\r\n    \"type\": \"ColorizeCommand\",\r\n    \"parameters\": [\r\n      {\r\n        \"type\": \"ColorParameter\",\r\n        \"value\": \"Rouge\"\r\n      }\r\n    ]\r\n  }\r\n]";
            return DeserializeCommand(jsonTest);
        }

        private List<Command> CommandTest2()
        {
            string jsonTest = "[\r\n  {\r\n    \"type\": \"ColorizeCommand\",\r\n    \"parameters\": [\r\n      {\r\n        \"type\": \"ColorParameter\",\r\n        \"value\": \"Rouge\"\r\n      },\r\n      {\r\n        \"type\": \"SelectionParameter\",\r\n        \"filters\": [\r\n          {\r\n            \"operator\": \"=\",\r\n            \"type\": \"Annotation\",\r\n            \"value\": \"citrouille\",\r\n            \"timestamp\": \"2025-10-06T10:50:45.1472611+02:00\"\r\n          },\r\n          \"AND\",\r\n          {\r\n            \"operator\": \"=\",\r\n            \"type\": \"PointOfView\",\r\n            \"timestamp\": \"2025-10-06T10:50:46.4247752+02:00\"\r\n          }\r\n        ],\r\n        \"limit\": \"5\",\r\n        \"order\": {\r\n          \"criterias\": [\r\n            {\r\n              \"type\": \"size\",\r\n              \"desc\": true\r\n            },\r\n            {\r\n              \"type\": \"name\",\r\n              \"desc\": false\r\n            }\r\n          ]\r\n        }\r\n      }\r\n    ]\r\n  }\r\n]";
            return DeserializeCommand(jsonTest);
        }

        public async void PrintTest()
        {
            Debug.Log(JsonConvert.SerializeObject(CommandTest1()));
            Debug.Log(JsonConvert.SerializeObject(CommandTest2()));
            // debug turtle content of the graph
            Debug.Log(GraphManager.DecodeGraph(await CommandToGraphOutputCommandAsync(CommandTest2())));
        }

        public async Task<Graph> CommandToGraphOutputCommandAsync(List<Command> commands)
        {
            Graph graph = new();
            // import all ontologies in StreamingAssets/Ontologies (pour être optimal, il ne faudrait charger que l'ontologie des commandes)
            Dictionary<string, string> ontologies = await SvenSettings.GetOntologiesAsync();
            foreach (KeyValuePair<string, string> ontology in ontologies)
            {
                TurtleParser turtleParser = new();
                turtleParser.Load(graph, ontology.Value);
            }

            foreach (Command command in commands)
                await command.Semanticize(graph);

            return graph;
        }
    }
}