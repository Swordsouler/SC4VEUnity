using NaughtyAttributes;
using Newtonsoft.Json;
using Sc4ve.Multimodality.Intent;
using Sc4ve.Voice;
using Sven.Content;
using Sven.Context;
using Sven.GraphManagement;
using Sven.OwlTime;
using Sven.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
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

        private static readonly HttpClient _httpClient = new();

        // LLama-7b, qwen-3.5, mistral-nemo 
        // Le corps principal et statique du prompt est maintenant une constante.
        private const string SYSTEM_PROMPT_TEMPLATE = @"Tu es un système expert qui convertit le langage naturel en un format de commande JSON pour un environnement 3D.
Ta seule et unique réponse doit être le contenu JSON brut, sans explication ou formatage markdown.

--- FORMAT D'ENTRÉE ---
L'entrée utilisateur sera un objet JSON contenant le texte et une liste de mots avec leur horodatage.
{{
  ""Text"": ""Texte de la phrase"",
  ""Words"": [
    {{ ""Text"": ""mot1"", ""StartedAt"": ""2026-01-27T10:00:00.100Z"", ""EndedAt"": ""2026-01-27T10:00:00.500Z"" }},
    {{ ""Text"": ""mot2"", ""StartedAt"": ""2026-01-27T10:00:00.600Z"", ""EndedAt"": ""2026-01-27T10:00:00.900Z"" }}
  ]
}}

--- ERREURS FRÉQUENTES À ÉVITER ---
1.  RÈGLE D'OR (NON NÉGOCIABLE) : Si le type de commande est 'ColorizeCommand', le 'SelectionParameter' ne doit JAMAIS contenir un filtre de type 'Color'. La couleur cible va UNIQUEMENT dans le 'ColorParameter'. La seule exception est pour décrire un objet existant, comme 'la pomme QUI EST verte'. Les phrases comme '... en vert', '... en couleur verte' ou '... avec la couleur verte' NE SONT PAS des exceptions et ne doivent pas générer de filtre 'Color'.
2.  Pour une phrase comme 'colorie les légumes', NE PAS ajouter de filtre 'Event' pour '{pointerTerm}'. Il n'y a pas de mot déictique ('ce', 'cette', etc.), donc il n'y a pas de pointage.
3.  CORÉFÉRENCE EXCLUSIVE : Si la phrase contient UNIQUEMENT une commande suivie d'un pronom ('le', 'la', 'les', 'eux', 'celui-ci', etc.) sans description d'objet, c'est une coréférence. Le filtre 'Coreference' doit être SEUL dans la liste des filtres. AUCUN filtre 'Annotation' ne doit être ajouté.

--- COMMANDES DISPONIBLES ---
- ColorizeCommand: Applique une couleur. Paramètres: ColorParameter, SelectionParameter.
- MoveCommand: Déplace des objets. Paramètres: SelectionParameter (source), et soit PointParameter (destination) soit SelectionParameter (destination).
- SelectCommand / UnselectCommand: Sélectionne/désélectionne. Paramètres: SelectionParameter.
- ShowCommand / HideCommand: Affiche/masque. Paramètres: SelectionParameter.
- ScaleUpCommand / ScaleDownCommand: Change la taille. Paramètres: SelectionParameter.
- GrabCommand / ReleaseCommand: Saisit/relâche. Paramètres: SelectionParameter.
- MeasureCommand: Mesure une distance. Paramètres: multiples SelectionParameter et/ou PointParameter.
- SpeechCommand: Pose une question de clarification à l'utilisateur. Paramètres: SentenceParameter.

--- TYPES DE PARAMÈTRES ---
- 'SelectionParameter': Pour sélectionner des objets. Contient des filtres.
- 'PointParameter': Pour définir un point dans l'espace (souvent via un pointage).
- 'ColorParameter': Pour définir une couleur cible.
- 'SentenceParameter': Contient la phrase à prononcer par le système pour demander une clarification.

--- TYPES DE FILTRES ---
- 'Annotation': Pour filtrer par le nom ou le type général d'un objet (ex: 'Voiture', 'Pomme').
- 'Color': Pour filtrer des objets par leur couleur actuelle (ex: trouver une 'Pomme' qui est 'Verte').
- 'Event': Pour les événements système. Les valeurs valides sont '{pointerTerm}' et '{cameraTerm}'.
- 'Coreference': Pour faire référence à des objets d'une commande précédente (par exemple, en utilisant des pronoms comme 'les', 'eux', 'le'). La seule valeur valide est '{lastResultTerm}'.

--- VOCABULAIRE D'ANNOTATION CONNU ---
Lorsque tu utilises un filtre de type 'Annotation', la 'value' DOIT correspondre EXACTEMENT à l'un des termes de la liste {annotationTypesString}, sans le modifier (pas de pluriel, pas de changement de casse).

--- VOCABULAIRE DE COULEUR CONNU ---
Lorsque tu utilises un 'ColorParameter' ou un filtre de type 'Color', la 'value' DOIT être l'une des suivantes : {availableColorsString}.

--- MOTS DÉICTIQUES DE POINTAGE CONNUS ---
Les mots déictiques valides pour faire référence au pointage sont : {pointerDeicticsString}

NOTE: Dans les exemples suivants, la propriété 'StartedAt' est omise pour des raisons de concision, mais elle sera présente dans l'entrée utilisateur réelle.

--- EXEMPLES ---

## EXEMPLE 1: Masquer un objet spécifique (décrit par sa couleur)
Entrée utilisateur:
{{""Text"":""masque la voiture rouge"",""Words"":[{{""Text"":""masque"",""EndedAt"":""2026-01-27T12:30:01.500Z""}},{{""Text"":""la"",""EndedAt"":""2026-01-27T12:30:01.650Z""}},{{""Text"":""voiture"",""EndedAt"":""2026-01-27T12:30:02.100Z""}},{{""Text"":""rouge"",""EndedAt"":""2026-01-27T12:30:02.500Z""}}]}}
JSON Attendu:
[
  {{
    ""type"": ""HideCommand"",
    ""parameters"": [
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Annotation"", ""value"": ""Voiture"", ""timestamp"": ""2026-01-27T12:30:02.100Z"" }},
          ""AND"",
          {{ ""type"": ""Color"", ""value"": ""Rouge"", ""timestamp"": ""2026-01-27T12:30:02.500Z"" }}
        ],
        ""limit"": ""1""
      }}
    ]
  }}
]

## EXEMPLE 5: Filtre combiné (Annotation ET Couleur)
Entrée utilisateur:
{{""Text"":""colorie en rouge cette pomme verte"",""Words"":[{{""Text"":""colorie"",""EndedAt"":""2026-01-27T12:34:01.500Z""}},{{""Text"":""en"",""EndedAt"":""2026-01-27T12:34:01.600Z""}},{{""Text"":""rouge"",""EndedAt"":""2026-01-27T12:34:02.000Z""}},{{""Text"":""cette"",""EndedAt"":""2026-01-27T12:34:02.300Z""}},{{""Text"":""pomme"",""EndedAt"":""2026-01-27T12:34:02.700Z""}},{{""Text"":""verte"",""EndedAt"":""2026-01-27T12:34:03.100Z""}}]}}
JSON Attendu:
[
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
          {{ ""type"": ""Annotation"", ""value"": ""Pomme"", ""timestamp"": ""2026-01-27T12:34:02.700Z"" }},
          ""AND"",
          {{ ""type"": ""Color"", ""value"": ""Vert"", ""timestamp"": ""2026-01-27T12:34:03.100Z"" }}
        ],
        ""limit"": ""1""
      }}
    ]
  }}
]

## EXEMPLE 9: Commande de colorisation simple (CIBLE)
Entrée utilisateur:
{{""Text"":""mets les pommes en bleu"",""Words"":[{{""Text"":""mets"",""EndedAt"":""2026-01-29T17:42:52.051Z""}},{{""Text"":""les"",""EndedAt"":""2026-01-29T17:42:52.211Z""}},{{""Text"":""pommes"",""EndedAt"":""2026-01-29T17:42:52.601Z""}},{{""Text"":""en"",""EndedAt"":""2026-01-29T17:42:52.751Z""}},{{""Text"":""bleu"",""EndedAt"":""2026-01-29T17:42:53.101Z""}}]}}
JSON Attendu:
[
  {{
    ""type"": ""ColorizeCommand"",
    ""parameters"": [
      {{
        ""type"": ""ColorParameter"",
        ""value"": ""Bleu""
      }},
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Annotation"", ""value"": ""Pomme"", ""timestamp"": ""2026-01-29T17:42:52.601Z"" }}
        ],
        ""limit"": ""-1""
      }}
    ]
  }}
]

## EXEMPLE 10: Commande de colorisation avec 'toutes' et 'couleur' (CIBLE)
Entrée utilisateur:
{{""Text"":""coloris toutes les citrouilles en couleur verte"",""Words"":[{{""Text"":""coloris"",""EndedAt"":""2026-02-02T16:10:01.000Z""}},{{""Text"":""toutes"",""EndedAt"":""2026-02-02T16:10:01.400Z""}},{{""Text"":""les"",""EndedAt"":""2026-02-02T16:10:01.600Z""}},{{""Text"":""citrouilles"",""EndedAt"":""2026-02-02T16:10:02.200Z""}},{{""Text"":""en"",""EndedAt"":""2026-02-02T16:10:02.300Z""}},{{""Text"":""couleur"",""EndedAt"":""2026-02-02T16:10:02.700Z""}},{{""Text"":""verte"",""EndedAt"":""2026-02-02T16:10:03.100Z""}}]}}
JSON Attendu:
[
  {{
    ""type"": ""ColorizeCommand"",
    ""parameters"": [
      {{
        ""type"": ""ColorParameter"",
        ""value"": ""Vert""
      }},
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Annotation"", ""value"": ""Citrouille"", ""timestamp"": ""2026-02-02T16:10:02.200Z"" }}
        ],
        ""limit"": ""-1""
      }}
    ]
  }}
]

## EXEMPLE 11: Commande de déplacement avec double déictique ('ça', 'ici')
Entrée utilisateur:
{{""Text"":""déplace ça ici"",""Words"":[{{""Text"":""déplace"",""EndedAt"":""2026-02-02T17:20:01.000Z""}},{{""Text"":""ça"",""EndedAt"":""2026-02-02T17:20:01.500Z""}},{{""Text"":""ici"",""EndedAt"":""2026-02-02T17:20:02.000Z""}}]}}
JSON Attendu:
[
  {{
    ""type"": ""MoveCommand"",
    ""parameters"": [
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Event"", ""value"": ""{pointerTerm}"", ""timestamp"": ""2026-02-02T17:20:01.500Z"" }}
        ],
        ""limit"": ""1""
      }},
      {{
        ""type"": ""PointParameter"",
        ""value"": ""{pointerTerm}"",
        ""timestamp"": ""2026-02-02T17:20:02.000Z""
      }}
    ]
  }}
]

## EXEMPLE 13: Coréférence pour colorier des objets précédemment sélectionnés
Contexte: L'utilisateur a d'abord dit ""sélectionne les pommes"". Maintenant il dit :
Entrée utilisateur:
{{""Text"":""colorie les en vert"",""Words"":[{{""Text"":""colorie"",""EndedAt"":""2026-02-04T11:00:01.000Z""}},{{""Text"":""les"",""EndedAt"":""2026-02-04T11:00:01.500Z""}},{{""Text"":""en"",""EndedAt"":""2026-02-04T11:00:01.700Z""}},{{""Text"":""vert"",""EndedAt"":""2026-02-04T11:00:02.200Z""}}]}}
JSON Attendu:
[
  {{
    ""type"": ""ColorizeCommand"",
    ""parameters"": [
      {{
        ""type"": ""ColorParameter"",
        ""value"": ""Vert""
      }},
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Coreference"", ""timestamp"": ""2026-02-04T11:00:01.500Z"" }}
        ],
        ""limit"": ""-1""
      }}
    ]
  }}
]
--- FIN DES EXEMPLES ---
";

        private Task _initializationTask;
        private string _annotationTypesString;
        private string _availableColorsString;
        private string _cameraNamesString;
        private string _pointerNamesString;
        private string _pointerDeicticsString;

        private async void Awake()
        {
            UserData.Language = _language;
            if (_voskSpeechToText != null) _voskSpeechToText.OnTranscriptionResult += OnTranscriptionResult;

            //await TextToSpeechController.Initialize();
            //await TextToSpeechController.GenerateAndPlaySpeech("Ceci est un test pour vérifier que le système de synthèse vocale fonctionne correctement.");
        }

        private async void OnTranscriptionResult(string obj)
        {
            Debug.Log($"[LLM] Received transcription result: {obj}");
            var result = new RecognitionResult(obj);
            for (int i = 0; i < result.Phrases.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(result.Phrases[i].Text)) continue;

                Sentence phrase = result.Phrases[i];
                phrase.Start(new Instant(phrase.StartedAt));
                phrase.End(new Instant(phrase.EndedAt));

                try
                {
                    Debug.Log($"[LLM] Sending sentence for analysis: \"{phrase.Text}\"");

                    string commandJson = await GetValidatedCommandJsonFromLlmAsync(phrase);

                    if (string.IsNullOrWhiteSpace(commandJson))
                    {
                        Debug.LogWarning("[LLM] Received empty or null JSON from LLM after all attempts.");
                        continue;
                    }

                    Debug.Log($"[LLM] Received FINAL JSON: {commandJson}");
                    List<Command> commands = DeserializeCommand(commandJson);
                    if (commands == null) continue;

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

        /// <summary>
        /// Orchestrateur de l'approche hybride. Tente une requête rapide avec GPT-3.5,
        /// la valide, et ne passe à GPT-4 qu'en cas d'erreur connue.
        /// </summary>
        private async Task<string> GetValidatedCommandJsonFromLlmAsync(Sentence sentence)
        {
            // 1. Essai rapide avec GPT-3.5
            Debug.Log("[LLM] Attempting fast path with gpt-3.5-turbo...");
            string jsonResponse = await CallLlmApiAsync(sentence, "gpt-3.5-turbo");

            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                Debug.LogError("[LLM] GPT-3.5 returned an empty response.");
                return null; // Échec précoce
            }

            // 2. Validation de la réponse
            List<Command> commands = DeserializeCommand(jsonResponse);
            if (commands == null) return null;

            bool needsCorrection = false;

            // Règle 1: Vérifier la présence incorrecte d'un filtre de couleur dans une ColorizeCommand
            if (commands.Any(c => c is ColorizeCommand && (c.Parameters.OfType<SelectionParameter>().FirstOrDefault()?.Filters.Any(f => f.Condition?.Type == "Color") ?? false)))
            {
                needsCorrection = true;
            }

            // Règle 2: Vérifier l'absence de filtre pointeur si un mot déictique est présent
            if (!needsCorrection)
            {
                // split ", "
                HashSet<string> deicticWords = new(_pointerDeicticsString.Split(", ").Select(s => s.Trim('\'').ToLower()));
                bool sentenceHasDeictic = deicticWords.Any(word => sentence.Text.ToLower().Contains(word));

                if (sentenceHasDeictic)
                {
                    var allSelectionParams = commands.SelectMany(c => c.Parameters.OfType<SelectionParameter>());
                    if (allSelectionParams.Any() && allSelectionParams.All(sp => !sp.Filters.Any(f => f.Condition?.Type == "Event" && f.Condition?.Value == _pointerNamesString)))
                    {
                        Debug.Log("[LLM] Validation failed: Deictic word found but pointer event filter is missing.");
                        needsCorrection = true;
                    }
                }
            }

            // 3. Si la validation échoue, on corrige avec GPT-4
            if (needsCorrection)
            {
                Debug.LogWarning("[LLM] GPT-3.5 response failed validation. Retrying with gpt-4-turbo for accuracy.");
                jsonResponse = await CallLlmApiAsync(sentence, "gpt-4-turbo");
            }
            else
            {
                Debug.Log("[LLM] GPT-3.5 response passed validation. Using fast path result.");
            }

            return jsonResponse;
        }

        private Task InitializeVocabulariesAsync()
        {
            _initializationTask ??= DoInitializeVocabulariesAsync();
            return _initializationTask;
        }

        private async Task DoInitializeVocabulariesAsync()
        {
            Debug.Log("[LLM] Initializing and caching vocabularies...");
            var annotationTypesTask = ISemanticAnnotation.GetAvailableTypesAsync(UserData.Locale);
            var availableColorsTask = ColorParameter.GetAvailableColorsAsync();
            var pointerDeicticsTask = Pointer.GetAllAvailableDeictics(UserData.Locale);
            var pointerNameTask = Pointer.GetAllAvailableNames(UserData.Locale);
            var cameraNameTask = PointOfView.GetAllAvailableNames(UserData.Locale);

            await Task.WhenAll(annotationTypesTask, availableColorsTask, pointerDeicticsTask, pointerNameTask, cameraNameTask);

            List<string> annotationTypes = await annotationTypesTask;
            _annotationTypesString = string.Join(", ", annotationTypes.Select(t => $"{t}"));

            List<string> availableColors = await availableColorsTask;
            _availableColorsString = string.Join(", ", availableColors.Select(c => $"{c}"));

            List<string> pointerDeictics = await pointerDeicticsTask;
            _pointerDeicticsString = string.Join(", ", pointerDeictics.Select(d => $"{d}"));

            List<string> pointerNames = await pointerNameTask;
            _pointerNamesString = string.Join(", ", pointerNames.Select(n => $"{n}"));

            List<string> cameraNames = await cameraNameTask;
            _cameraNamesString = string.Join(", ", cameraNames.Select(n => $"{n}"));

            Debug.Log("[LLM] Vocabularies cached.");
        }

        /// <summary>
        /// Appelle l'API OpenAI avec le modèle et la phrase spécifiés.
        /// </summary>
        private async Task<string> CallLlmApiAsync(Sentence sentence, string model)
        {
            if (string.IsNullOrWhiteSpace(_openAiApiKey))
            {
                Debug.LogError("[LLM] OpenAI API Key is not set.");
                return null;
            }

            // S'assure que les vocabulaires sont initialisés avant de continuer
            await InitializeVocabulariesAsync();

            // Construction du prompt final à partir du template
            string finalSystemPrompt = SYSTEM_PROMPT_TEMPLATE
                .Replace("{annotationTypesString}", _annotationTypesString)
                .Replace("{availableColorsString}", _availableColorsString)
                .Replace("{cameraTerm}", _cameraNamesString)
                .Replace("{pointerTerm}", _pointerNamesString)
                .Replace("{pointerDeicticsString}", _pointerDeicticsString);

            var userInput = new { sentence.Text, sentence.Words };
            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = finalSystemPrompt },
                    new { role = "user", content = JsonConvert.SerializeObject(userInput) }
                },
                temperature = 0.1
            };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);
            requestMessage.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(requestMessage);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                Debug.LogError($"[LLM] API Error ({model}): {response.StatusCode}\n{errorBody}");
                return null;
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            var openAiResponse = JsonConvert.DeserializeObject<OpenAiResponse>(responseBody);

            if (openAiResponse?.Usage != null)
            {
                var usage = openAiResponse.Usage;
                Debug.Log($"[LLM] Token Usage ({model}): Prompt={usage.PromptTokens}, Completion={usage.CompletionTokens}, Total={usage.TotalTokens}");
            }

            return openAiResponse?.Choices?[0]?.Message?.Content;
        }

        private List<Command> DeserializeCommand(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<List<Command>>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LLM] JSON Deserialization failed: {e.Message}\nJSON was: {json}");
                return null;
            }
        }

        public async Task<List<Command>> CommandToGraphOutputCommandAsync(List<Command> commands)
        {
            return await Task.Run(async () =>
            {
                Graph graph = new();
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
            List<SemantizationCore> lastObjects = new();
            foreach (Command command in commands)
            {
                lastObjects.AddRange(command.Execute());
            }
            Command.LastObjects = lastObjects;
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
        ""timestamp"": ""{DateTime.Now:o}""
      }},
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{
            ""type"": ""Event"",
            ""value"": ""Pointeur"",
            ""timestamp"": ""{DateTime.Now:o}""
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
            ""timestamp"": ""{DateTime.Now:o}""
         }},
        ""OR"",
        {{
            ""type"": ""Annotation"",
            ""value"": ""Pomme"",
            ""timestamp"": ""{DateTime.Now:o}""
          }},
          ""AND"",
          {{
            ""type"": ""Event"",
            ""value"": ""Caméra"",
            ""timestamp"": ""{DateTime.Now:o}""
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