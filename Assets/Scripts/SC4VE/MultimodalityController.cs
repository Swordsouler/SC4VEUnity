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
            List<Command> commands = await CommandToGraphOutputCommandAsync(CommandTest2());
            await ResolveCommands(commands);
            Debug.Log("Command has been resolved");
        }

        public async Task<List<Command>> CommandToGraphOutputCommandAsync(List<Command> commands)
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

            GraphManager.Assert(graph.Triples);

            return commands;
        }

        public async Task ResolveCommands(List<Command> commands)
        {
            foreach (Command command in commands)
            {
                command.Execute();
            }

            /*
            string sparqlUpdate = @"PREFIX : <https://sven.lisn.upsaclay.fr/ve/Buffer/>
PREFIX time: <http://www.w3.org/2006/time#>
PREFIX sven: <https://sven.lisn.upsaclay.fr/ontology#>
PREFIX sc4ve: <https://sc4ve.lisn.upsaclay.fr/ontology#>

INSERT {
    ?newInstant a time:Instant ;
    			time:inXSDDateTime ?newInstantTime .
    ?newColorInterval a time:Interval ;
    				  time:before ?currentColorInterval ;
    				  time:hasBeginning ?newInstant .
    ?newColor a sven:Color ;
    		  sven:exactType sven:Color ;
        	  sven:hasTemporalExtent ?newColorInterval ;
    		  sven:r ?r ;
    		  sven:g ?g ;
    		  sven:b ?b ;
    		  sven:a ?a .
    ?currentColorInterval time:before ?newColorInterval ;
    					  time:hasEnd ?newInstant ;
    					  time:hasDuration ?currentColorDuration .
    ?render sven:color ?newColor .
}
WHERE {
    ?command a sc4ve:ColorizeCommand .
    
    # selection parameter
    ?command sc4ve:hasParameter ?selectionParameter .
    ?selectionParameter a sc4ve:SelectionParameter ;
    					sven:value ?object .
    ?object sven:component ?render .
    ?render a sven:3DRender ;
    		sven:color ?currentColor .
    ?currentColor sven:hasTemporalExtent ?currentColorInterval .
    ?currentColorInterval time:hasBeginning ?currentColorStartInstant .
    ?currentColorStartInstant time:inXSDDateTime ?currentColorStartTime .
    
    # color parameter
    ?command sc4ve:hasParameter ?colorParameter .
	?colorParameter a sc4ve:ColorParameter ;
    				sven:r ?r ;
    				sven:g ?g ;
    				sven:b ?b ;
    				sven:a ?a .
    
    BIND(URI(CONCAT(STR(:), STRUUID())) AS ?newInstant)
    BIND(URI(CONCAT(STR(:), STRUUID())) AS ?newColorInterval)
    BIND(URI(CONCAT(STR(:), STRUUID())) AS ?newColor)
    BIND(?newInstantTime - ?currentColorStartTime AS ?currentColorDuration)
    
    {
        SELECT DISTINCT ?currentColorInterval ?newInstantTime
        WHERE {
    		BIND(NOW() AS ?newInstantTime)
            ?currentColorInterval a time:Interval ;
            time:hasBeginning/time:inXSDDateTime ?startTime .
            OPTIONAL {
                ?currentColorInterval time:hasEnd/time:inXSDDateTime ?_endTime .
            }
            BIND(IF(BOUND(?_endTime), ?_endTime, NOW()) AS ?endTime)
            FILTER(?startTime <= ?newInstantTime && ?newInstantTime <= ?endTime)
        } ORDER BY ?startTime ?endTime limit 10000
    }
}";
            await GraphManager.UpdateMemoryAsync(sparqlUpdate);
            await GraphManager.ForceFlushToEndpointAsync();
            await GraphManager.SynchronizeAsync();*/
        }
    }
}