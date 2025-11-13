using NaughtyAttributes;
using Newtonsoft.Json;
using Sc4ve.Multimodality.Parameter;
using Sc4ve.Voice;
using Sven.GraphManagement;
using Sven.OwlTime;
using Sven.Utils;
using System;
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
            string jsonTest = $@"[
  {{
    ""type"": ""ColorizeCommand"",
    ""parameters"": [
      {{
        ""type"": ""ColorParameter"",
        ""value"": ""Rouge""
      }},
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
   {{
            ""type"": ""Annotation"",
            ""value"": ""Citrouille"",
            ""timestamp"": ""{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}""
         }},
   ""OR"",
   {{
            ""type"": ""Annotation"",
            ""value"": ""Pomme"",
            ""timestamp"": ""{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}""
          }},
          ""AND"",
          {{
    ""type"": ""Event"",
    ""value"": ""Caméra"",
            ""timestamp"": ""{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}""
          }}
        ],
        ""limit"": ""5"",
        ""order"": {{
          ""criterias"": [
            {{
              ""type"": ""size"",
              ""desc"": true
            }},
            {{
              ""type"": ""name"",
              ""desc"": false
            }}
          ]
        }}
      }}
    ]
  }}
]";
            return DeserializeCommand(jsonTest);
        }

        public async void PrintTest()
        {
            Debug.Log(JsonConvert.SerializeObject(CommandTest1()));
            Debug.Log(JsonConvert.SerializeObject(CommandTest2()));
            // debug turtle content of the graph
            Graph graph = await CommandToGraphOutputCommandAsync(CommandTest2());
            Debug.Log(GraphManager.DecodeGraph(graph));
            GraphManager.Assert(graph.Triples);
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
            graph.BaseUri = new Uri(SvenSettings.BaseUri);
            graph.NamespaceMap.AddNamespace("", UriFactory.Create(SvenSettings.BaseUri));
            foreach (Command command in commands)
                await command.Semanticize(graph);

            return graph;
        }
    }
}