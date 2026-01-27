using NaughtyAttributes;
using Newtonsoft.Json;
using Sc4ve.Multimodality.Intent;
using Sc4ve.Voice;
using Sven.GraphManagement;
using Sven.OwlTime;
using Sven.Utils;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace Sc4ve.Multimodality
{
    public class MultimodalityController : MonoBehaviour
    {
        [BoxGroup("References"), SerializeField] private VoskSpeechToText _voskSpeechToText;
        [BoxGroup("References"), SerializeField] private Language _language = Language.English;

        [BoxGroup("LLM Settings"), SerializeField, Tooltip("Clé API OpenAI. Ne pas exposer publiquement.")]
        private string _openAiApiKey;

        private void Awake()
        {
            UserData.Language = _language;
            if (_voskSpeechToText != null) _voskSpeechToText.OnTranscriptionResult += OnTranscriptionResult;
        }

        private async void OnTranscriptionResult(string obj)
        {
            var result = new RecognitionResult(obj);
            for (int i = 0; i < result.Phrases.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(result.Phrases[i].Text)) continue;

                Sentence phrase = result.Phrases[i];
                phrase.Start(new Instant(phrase.StartedAt));
                phrase.End(new Instant(phrase.EndedAt));

                // Process the phrase into commands using the LLM
                try
                {
                    Debug.Log($"[LLM] Sending text for analysis: \"{phrase.Text}\"");
                    string commandJson = await GetCommandJsonFromTextWithLlmAsync(phrase.Text);

                    if (string.IsNullOrWhiteSpace(commandJson))
                    {
                        Debug.LogWarning("[LLM] Received empty or null JSON from LLM.");
                        continue;
                    }

                    Debug.Log($"[LLM] Received JSON: {commandJson}");
                    List<Command> commands = DeserializeCommand(commandJson);
                    await CommandToGraphOutputCommandAsync(commands);
                    ResolveCommands(commands);
                    Debug.Log("[LLM] Commands resolved successfully.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LLM] An error occurred during LLM processing: {e.Message}\n{e.StackTrace}");
                }
            }
        }
        private List<Command> DeserializeCommand(string json)
        {
            // using newtonsoft json
            List<Command> commands = JsonConvert.DeserializeObject<List<Command>>(json);
            return commands;
        }

        public async Task<List<Command>> CommandToGraphOutputCommandAsync(List<Command> commands)
        {
            return await Task.Run(async () =>
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
            });
        }

        public void ResolveCommands(List<Command> commands)
        {
            foreach (Command command in commands)
            {
                command.Execute();
            }
        }

        /// <summary>
        /// Sends text to an LLM to get a command JSON structure.
        /// </summary>
        private async Task<string> GetCommandJsonFromTextWithLlmAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(_openAiApiKey))
            {
                Debug.LogError("[LLM] OpenAI API Key is not set in the inspector.");
                return null;
            }

            string systemPrompt = $@"Tu es un système expert qui convertit le langage naturel en un format de commande JSON spécifique pour un environnement 3D.
Ta seule et unique réponse doit être le contenu JSON brut, sans explication, formatage markdown (comme ```json) ou tout autre texte.

--- COMMANDES DISPONIBLES ---
- ColorizeCommand: Applique une couleur à des objets. Paramètres: ColorParameter, SelectionParameter.
- MoveCommand: Déplace des objets vers un point ou un autre objet. Paramètres: SelectionParameter (source), et soit PointParameter (destination) soit SelectionParameter (destination).
- SelectCommand / UnselectCommand: Sélectionne ou désélectionne des objets. Paramètres: SelectionParameter.
- ShowCommand / HideCommand: Affiche ou masque des objets. Paramètres: SelectionParameter.
- ScaleUpCommand / ScaleDownCommand: Agrandit ou réduit la taille des objets. Paramètres: SelectionParameter.
- GrabCommand / ReleaseCommand: Saisit ou relâche des objets. Paramètres: SelectionParameter.
- MeasureCommand: Mesure une distance entre des points ou des objets. Paramètres: multiples SelectionParameter et/ou PointParameter.

--- PARAMETRES ---
- Le 'SelectionParameter' est complexe. Il contient 'filters' (liste de conditions comme 'Annotation' pour le nom, 'Event' pour un événement système), 'limit' (nombre max d'objets), et 'order' (tri par 'size' ou 'name').
- Les filtres peuvent être combinés avec les opérateurs ""AND"" et ""OR"".
- La date et l'heure ('timestamp') doivent être au format ISO 8601 : {DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}

--- EXEMPLES ---

## EXEMPLE 1: Commande de coloration complexe
Texte utilisateur: ""Colorie en rouge les cinq plus grosses citrouilles ou pomme que je vois""
JSON Attendu:
[
  {{
    ""type"": ""ColorizeCommand"",
    ""parameters"": [
      {{ ""type"": ""ColorParameter"", ""value"": ""Rouge"" }},
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Annotation"", ""value"": ""Citrouille"", ""timestamp"": ""{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}"" }},
          ""OR"",
          {{ ""type"": ""Annotation"", ""value"": ""Pomme"", ""timestamp"": ""{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}"" }},
          ""AND"",
          {{ ""type"": ""Event"", ""value"": ""Caméra"", ""timestamp"": ""{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}"" }}
        ],
        ""limit"": ""5"",
        ""order"": {{ ""criterias"": [ {{ ""type"": ""size"", ""desc"": true }} ] }}
      }}
    ]
  }}
]

## EXEMPLE 2: Commande de déplacement simple
Texte utilisateur: ""Déplace la caisse vers le pointeur""
JSON Attendu:
[
  {{
    ""type"": ""MoveCommand"",
    ""parameters"": [
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [ {{ ""type"": ""Annotation"", ""value"": ""Caisse"", ""timestamp"": ""{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}"" }} ],
        ""limit"": ""1""
      }},
      {{ ""type"": ""PointParameter"", ""value"": ""Pointer"", ""timestamp"": ""{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}"" }}
    ]
  }}
]

## EXEMPLE 3: Commande pour masquer un objet
Texte utilisateur: ""Masque cette voiture""
JSON Attendu:
[
  {{
    ""type"": ""HideCommand"",
    ""parameters"": [
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [ {{ ""type"": ""Annotation"", ""value"": ""Voiture"", ""timestamp"": ""{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}"" }} ],
        ""limit"": ""1""
      }}
    ]
  }}
]

## EXEMPLE 4: Commande de sélection avec tri
Texte utilisateur: ""Sélectionne les deux plus petites sphères""
JSON Attendu:
[
  {{
    ""type"": ""SelectCommand"",
    ""parameters"": [
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [ {{ ""type"": ""Annotation"", ""value"": ""Sphère"", ""timestamp"": ""{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}"" }} ],
        ""limit"": ""2"",
        ""order"": {{ ""criterias"": [ {{ ""type"": ""size"", ""desc"": false }} ] }}
      }}
    ]
  }}
]
--- FIN DES EXEMPLES ---
";

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);

            var requestBody = new
            {
                model = "gpt-3.5-turbo", // Modèle rapide, bon pour commencer.
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = text }
                },
                temperature = 0.1 // Faible température pour des résultats plus déterministes.
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                Debug.LogError($"[LLM] API Error: {response.StatusCode}\n{errorBody}");
                return null;
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            // NOUVEAU LOG: Affiche la réponse brute complète de l'API
            Debug.Log($"[LLM] Full API response body:\n{responseBody}");

            var openAiResponse = JsonConvert.DeserializeObject<OpenAiResponse>(responseBody);

            // NOUVEAU LOG: Affiche l'utilisation des tokens
            if (openAiResponse?.Usage != null)
            {
                var usage = openAiResponse.Usage;
                Debug.Log($"[LLM] Token Usage: Prompt={usage.PromptTokens}, Completion={usage.CompletionTokens}, Total={usage.TotalTokens}");
            }

            return openAiResponse?.Choices?[0]?.Message?.Content;
        }

        // Classes d'aide pour désérialiser la réponse d'OpenAI
        private class OpenAiResponse
        {
            public List<Choice> Choices { get; set; }
            public Usage Usage { get; set; }
        }
        private class Choice { public Message Message { get; set; } }
        private class Message { public string Role { get; set; } public string Content { get; set; } }
        private class Usage
        {
            [JsonProperty("prompt_tokens")]
            public int PromptTokens { get; set; }
            [JsonProperty("completion_tokens")]
            public int CompletionTokens { get; set; }
            [JsonProperty("total_tokens")]
            public int TotalTokens { get; set; }
        }


        #region TestCommands

        private List<Command> CommandTest1()
        {
            string jsonTest = $@"[
  {{
    ""type"": ""MoveCommand"",
    ""parameters"": [
      {{
        ""type"": ""PointParameter"",
        ""value"": ""Pointer"",
   ""timestamp"": ""{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}""
      }},
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{
            ""type"": ""Event"",
            ""value"": ""Pointeur"",
            ""timestamp"": ""{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}""
          }}
        ],
        ""limit"": ""1""
      }}
    ]
  }}
]";
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
            /*Debug.Log(JsonConvert.SerializeObject(CommandTest1()));
            Debug.Log(JsonConvert.SerializeObject(CommandTest2()));
            // debug turtle content of the graph
            List<Command> commands = await CommandToGraphOutputCommandAsync(CommandTest1());
            ResolveCommands(commands);
            Debug.Log("Command has been resolved");*/
            Debug.Log(JsonConvert.SerializeObject(new Sentence("Colorie en rouge les cinq plus grosses citrouilles ou pomme que je vois")));
        }

        private void Update()
        {
            HandlePointerDown();
            HandlePointerUp();
        }

        private Parameter thisParameter = null;
        private Parameter thereParameter = null;
        private bool _isResolvingCommand = false;

        public void HandlePointerDown()
        {
            if (Input.GetMouseButtonDown(0))
            {
                thisParameter = new SelectionParameter
                {
                    Filters = new List<FilterElement>
                    {
                        new() {
                            Condition = new Condition
                            {
                                Type = "Event",
                                Value = "Pointeur",
                                Timestamp = DateTime.Now,
                            },
                        }
                    },
                    Limit = 1,
                };
            }
        }

        public async void HandlePointerUp()
        {
            if (Input.GetMouseButtonUp(0))
            {
                thereParameter = new PointParameter
                {
                    Value = "Pointer",
                    Timestamp = DateTime.Now,
                };
                Command moveCommand;
                moveCommand = new MoveCommand
                {
                    Parameters = new List<Parameter>
                    {
                        thisParameter,
                        thereParameter,
                    }
                };
                List<Command> commands = new() { moveCommand };
                thisParameter = null;
                thereParameter = null;
                if (_isResolvingCommand) return;
                _isResolvingCommand = true;
                await CommandToGraphOutputCommandAsync(commands);
                ResolveCommands(commands);
                Debug.Log(JsonConvert.SerializeObject(commands));
                _isResolvingCommand = false;
            }
        }

        #endregion
    }
}