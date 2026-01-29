using NaughtyAttributes;
using Newtonsoft.Json;
using Sc4ve.Multimodality.Intent;
using Sc4ve.Voice;
using Sven.Content;
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
                    Debug.Log($"[LLM] Sending sentence for analysis: \"{phrase.Text}\"");
                    string commandJson = await GetCommandJsonFromTextWithLlmAsync(phrase);

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
        private async Task<string> GetCommandJsonFromTextWithLlmAsync(Sentence sentence)
        {
            if (string.IsNullOrWhiteSpace(_openAiApiKey))
            {
                Debug.LogError("[LLM] OpenAI API Key is not set in the inspector.");
                return null;
            }

            // Récupère dynamiquement les types d'annotations sémantiques disponibles
            List<string> annotationTypes = await ISemanticAnnotation.GetAvailableTypesAsync(UserData.Locale);
            string annotationTypesString = string.Join(", ", annotationTypes.Select(t => $"'{t}'"));

            // Récupère dynamiquement les couleurs disponibles
            List<string> availableColors = await ColorParameter.GetAvailableColorsAsync();
            string availableColorsString = string.Join(", ", availableColors.Select(c => $"'{c}'"));

            // Définit les termes localisés pour les événements
            string cameraTerm = (UserData.Language == Language.French) ? "Caméra" : "Camera";
            string pointerTerm = (UserData.Language == Language.French) ? "Pointeur" : "Pointer";

            string systemPrompt = $@"Tu es un système expert qui convertit le langage naturel en un format de commande JSON pour un environnement 3D.
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

--- INSTRUCTIONS CRUCIALES ---
1.  Le 'timestamp' d'un filtre DOIT correspondre au 'EndedAt' du mot pertinent dans l'entrée.
2.  Le paramètre 'limit' est crucial : utilise ""-1"" pour les sélections plurielles ou générales (ex: 'les pommes', 'toutes les voitures'). Utilise ""1"" pour les sélections singulières ou spécifiques (ex: 'la pomme', 'cette voiture').
3.  Pour cibler ce que l'utilisateur regarde ou pointe, utilise un filtre 'Event'. Utilise la valeur '{cameraTerm}' pour la vision (ex: 'ce que je vois') et '{pointerTerm}' pour le pointage direct.
4.  N'utilise un filtre 'Event' avec la valeur '{pointerTerm}' QUE SI ET SEULEMENT SI un mot déictique (comme 'ce', 'cette', 'ceci', 'ça') est présent pour indiquer un pointage. Ne l'ajoute pas pour une sélection générale comme 'les pommes'.
5.  Toujours inclure un opérateur logique ('AND' ou 'OR') entre les filtres dans un 'SelectionParameter' quand il y a plusieurs filtres.
6.  RÈGLE DE LA COULEUR CIBLE : Si une commande change une couleur (ex: '... en bleu'), la couleur mentionnée est la CIBLE. Elle va UNIQUEMENT dans le 'ColorParameter'. N'ajoute JAMAIS cette couleur comme filtre de sélection, sauf si la phrase décrit explicitement un objet déjà coloré (ex: 'la pomme qui est verte').

--- ERREURS FRÉQUENTES À ÉVITER ---
1.  Pour une phrase comme 'mets les pommes en bleu', NE PAS ajouter de filtre de couleur à la sélection. La couleur 'Bleu' est une CIBLE, pas un descripteur. La sélection doit uniquement contenir un filtre 'Annotation' pour 'Pomme'.
2.  Pour une phrase comme 'colorie les légumes', NE PAS ajouter de filtre 'Event' pour '{pointerTerm}'. Il n'y a pas de mot déictique ('ce', 'cette', etc.), donc il n'y a pas de pointage.

--- COMMANDES DISPONIBLES ---
- ColorizeCommand: Applique une couleur. Paramètres: ColorParameter, SelectionParameter.
- MoveCommand: Déplace des objets. Paramètres: SelectionParameter (source), et soit PointParameter (destination) soit SelectionParameter (destination).
- SelectCommand / UnselectCommand: Sélectionne/désélectionne. Paramètres: SelectionParameter.
- ShowCommand / HideCommand: Affiche/masque. Paramètres: SelectionParameter.
- ScaleUpCommand / ScaleDownCommand: Change la taille. Paramètres: SelectionParameter.
- GrabCommand / ReleaseCommand: Saisit/relâche. Paramètres: SelectionParameter.
- MeasureCommand: Mesure une distance. Paramètres: multiples SelectionParameter et/ou PointParameter.

--- TYPES DE FILTRES ---
- 'Annotation': Pour filtrer par le nom ou le type général d'un objet (ex: 'Voiture', 'Pomme').
- 'Color': Pour filtrer des objets par leur couleur actuelle (ex: trouver une 'Pomme' qui est 'Verte').
- 'Event': Pour les événements système. Les valeurs valides sont '{pointerTerm}' et '{cameraTerm}'.

--- VOCABULAIRE D'ANNOTATION CONNU ---
Lorsque tu utilises un filtre de type 'Annotation', la 'value' DOIT correspondre EXACTEMENT à l'un des termes de la liste {annotationTypesString}, sans le modifier (pas de pluriel, pas de changement de casse).

--- VOCABULAIRE DE COULEUR CONNU ---
Lorsque tu utilises un 'ColorParameter' ou un filtre de type 'Color', la 'value' DOIT être l'une des suivantes : {availableColorsString}.

--- EXEMPLES ---

## EXEMPLE 1: Masquer un objet spécifique (décrit par sa couleur)
Entrée utilisateur:
{{""Text"":""masque la voiture rouge"",""Words"":[{{""Text"":""masque"",""StartedAt"":""2026-01-27T12:30:01.100Z"",""EndedAt"":""2026-01-27T12:30:01.500Z""}},{{""Text"":""la"",""StartedAt"":""2026-01-27T12:30:01.520Z"",""EndedAt"":""2026-01-27T12:30:01.650Z""}},{{""Text"":""voiture"",""StartedAt"":""2026-01-27T12:30:01.670Z"",""EndedAt"":""2026-01-27T12:30:02.100Z""}},{{""Text"":""rouge"",""StartedAt"":""2026-01-27T12:30:02.120Z"",""EndedAt"":""2026-01-27T12:30:02.500Z""}}]}}
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

## EXEMPLE 2: Déplacer un objet
Entrée utilisateur:
{{""Text"":""déplace la caisse ici"",""Words"":[{{""Text"":""déplace"",""StartedAt"":""2026-01-27T12:31:05.000Z"",""EndedAt"":""2026-01-27T12:31:05.500Z""}},{{""Text"":""la"",""StartedAt"":""2026-01-27T12:31:05.520Z"",""EndedAt"":""2026-01-27T12:31:05.650Z""}},{{""Text"":""caisse"",""StartedAt"":""2026-01-27T12:31:05.670Z"",""EndedAt"":""2026-01-27T12:31:06.100Z""}},{{""Text"":""ici"",""StartedAt"":""2026-01-27T12:31:06.120Z"",""EndedAt"":""2026-01-27T12:31:06.400Z""}}]}}
JSON Attendu:
[
  {{
    ""type"": ""MoveCommand"",
    ""parameters"": [
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [ {{ ""type"": ""Annotation"", ""value"": ""Caisse"", ""timestamp"": ""2026-01-27T12:31:06.100Z"" }} ],
        ""limit"": ""1""
      }},
      {{ ""type"": ""PointParameter"", ""value"": ""{pointerTerm}"", ""timestamp"": ""2026-01-27T12:31:06.400Z"" }}
    ]
  }}
]

## EXEMPLE 3: Sélection de 'tous' les objets
Entrée utilisateur:
{{""Text"":""cache toutes les sphères"",""Words"":[{{""Text"":""cache"",""StartedAt"":""2026-01-27T12:32:01.000Z"",""EndedAt"":""2026-01-27T12:32:01.400Z""}},{{""Text"":""toutes"",""StartedAt"":""2026-01-27T12:32:01.420Z"",""EndedAt"":""2026-01-27T12:32:01.800Z""}},{{""Text"":""les"",""StartedAt"":""2026-01-27T12:32:01.820Z"",""EndedAt"":""2026-01-27T12:32:01.950Z""}},{{""Text"":""sphères"",""StartedAt"":""2026-01-27T12:32:02.000Z"",""EndedAt"":""2026-01-27T12:32:02.500Z""}}]}}
JSON Attendu:
[
  {{
    ""type"": ""HideCommand"",
    ""parameters"": [
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Annotation"", ""value"": ""Sphère"", ""timestamp"": ""2026-01-27T12:32:02.500Z"" }}
        ],
        ""limit"": ""-1""
      }}
    ]
  }}
]

## EXEMPLE 4: Coloriser plusieurs objets avec tri
Entrée utilisateur:
{{""Text"":""colorie les deux plus grosses citrouilles en vert"",""Words"":[{{""Text"":""colorie"",""StartedAt"":""2026-01-27T12:33:01.000Z"",""EndedAt"":""2026-01-27T12:33:01.500Z""}},{{""Text"":""les"",""StartedAt"":""2026-01-27T12:33:01.520Z"",""EndedAt"":""2026-01-27T12:33:01.650Z""}},{{""Text"":""deux"",""StartedAt"":""2026-01-27T12:33:01.670Z"",""EndedAt"":""2026-01-27T12:33:01.900Z""}},{{""Text"":""plus"",""StartedAt"":""2026-01-27T12:33:01.920Z"",""EndedAt"":""2026-01-27T12:33:02.100Z""}},{{""Text"":""grosses"",""StartedAt"":""2026-01-27T12:33:02.120Z"",""EndedAt"":""2026-01-27T12:33:02.500Z""}},{{""Text"":""citrouilles"",""StartedAt"":""2026-01-27T12:33:02.520Z"",""EndedAt"":""2026-01-27T12:33:03.100Z""}},{{""Text"":""en"",""StartedAt"":""2026-01-27T12:33:03.120Z"",""EndedAt"":""2026-01-27T12:33:03.250Z""}},{{""Text"":""vert"",""StartedAt"":""2026-01-27T12:33:03.270Z"",""EndedAt"":""2026-01-27T12:33:03.600Z""}}]}}
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
          {{ ""type"": ""Annotation"", ""value"": ""Citrouille"", ""timestamp"": ""2026-01-27T12:33:03.100Z"" }}
        ],
        ""limit"": ""2"",
        ""order"": {{
          ""criterias"": [
            {{
              ""type"": ""size"",
              ""desc"": true
            }}
          ]
        }}
      }}
    ]
  }}
]

## EXEMPLE 5: Filtre combiné (Annotation ET Couleur)
Entrée utilisateur:
{{""Text"":""colorie en rouge cette pomme verte"",""Words"":[{{""Text"":""colorie"",""StartedAt"":""2026-01-27T12:34:01.000Z"",""EndedAt"":""2026-01-27T12:34:01.500Z""}},{{""Text"":""en"",""StartedAt"":""2026-01-27T12:34:01.520Z"",""EndedAt"":""2026-01-27T12:34:01.600Z""}},{{""Text"":""rouge"",""StartedAt"":""2026-01-27T12:34:01.620Z"",""EndedAt"":""2026-01-27T12:34:02.000Z""}},{{""Text"":""cette"",""StartedAt"":""2026-01-27T12:34:02.020Z"",""EndedAt"":""2026-01-27T12:34:02.300Z""}},{{""Text"":""pomme"",""StartedAt"":""2026-01-27T12:34:02.320Z"",""EndedAt"":""2026-01-27T12:34:02.700Z""}},{{""Text"":""verte"",""StartedAt"":""2026-01-27T12:34:02.720Z"",""EndedAt"":""2026-01-27T12:34:03.100Z""}}]}}
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

## EXEMPLE 6: Sélectionner l'objet pointé
Entrée utilisateur:
{{""Text"":""sélectionne ça"",""Words"":[{{""Text"":""sélectionne"",""StartedAt"":""2026-01-27T12:35:01.000Z"",""EndedAt"":""2026-01-27T12:35:01.600Z""}},{{""Text"":""ça"",""StartedAt"":""2026-01-27T12:35:01.620Z"",""EndedAt"":""2026-01-27T12:35:01.900Z""}}]}}
JSON Attendu:
[
  {{
    ""type"": ""SelectCommand"",
    ""parameters"": [
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Event"", ""value"": ""{pointerTerm}"", ""timestamp"": ""2026-01-27T12:35:01.900Z"" }}
        ],
        ""limit"": ""1""
      }}
    ]
  }}
]

## EXEMPLE 7: Commande avec sélection par pointage ('cette')
Entrée utilisateur:
{{""Text"":""mets cette pomme en bleu"",""Words"":[{{""Text"":""mets"",""StartedAt"":""2026-01-29T14:49:19.123Z"",""EndedAt"":""2026-01-29T14:49:19.456Z""}},{{""Text"":""cette"",""StartedAt"":""2026-01-29T14:49:19.476Z"",""EndedAt"":""2026-01-29T14:49:19.789Z""}},{{""Text"":""pomme"",""StartedAt"":""2026-01-29T14:49:19.809Z"",""EndedAt"":""2026-01-29T14:49:20.200Z""}},{{""Text"":""en"",""StartedAt"":""2026-01-29T14:49:20.220Z"",""EndedAt"":""2026-01-29T14:49:20.350Z""}},{{""Text"":""bleu"",""StartedAt"":""2026-01-29T14:49:20.370Z"",""EndedAt"":""2026-01-29T14:49:20.700Z""}}]}}
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
          {{ ""type"": ""Annotation"", ""value"": ""Pomme"", ""timestamp"": ""2026-01-29T14:49:20.200Z"" }},
          ""AND"",
          {{ ""type"": ""Event"", ""value"": ""{pointerTerm}"", ""timestamp"": ""2026-01-29T14:49:20.200Z"" }}
        ],
        ""limit"": ""1""
      }}
    ]
  }}
]

## EXEMPLE 8: Sélection par vision ('que je vois')
Entrée utilisateur:
{{""Text"":""colorie en bleu toute la nourriture que je vois"",""Words"":[{{""Text"":""colorie"",""StartedAt"":""2026-01-29T15:01:57.900Z"",""EndedAt"":""2026-01-29T15:01:58.300Z""}},{{""Text"":""en"",""StartedAt"":""2026-01-29T15:01:58.320Z"",""EndedAt"":""2026-01-29T15:01:58.400Z""}},{{""Text"":""bleu"",""StartedAt"":""2026-01-29T15:01:58.420Z"",""EndedAt"":""2026-01-29T15:01:58.700Z""}},{{""Text"":""toute"",""StartedAt"":""2026-01-29T15:01:58.720Z"",""EndedAt"":""2026-01-29T15:01:59.000Z""}},{{""Text"":""la"",""StartedAt"":""2026-01-29T15:01:59.020Z"",""EndedAt"":""2026-01-29T15:01:59.100Z""}},{{""Text"":""nourriture"",""StartedAt"":""2026-01-29T15:01:59.120Z"",""EndedAt"":""2026-01-29T15:01:59.700Z""}},{{""Text"":""que"",""StartedAt"":""2026-01-29T15:01:59.720Z"",""EndedAt"":""2026-01-29T15:01:59.850Z""}},{{""Text"":""je"",""StartedAt"":""2026-01-29T15:01:59.870Z"",""EndedAt"":""2026-01-29T15:02:00.000Z""}},{{""Text"":""vois"",""StartedAt"":""2026-01-29T15:02:00.020Z"",""EndedAt"":""2026-01-29T15:02:00.300Z""}}]}}
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
          {{ ""type"": ""Annotation"", ""value"": ""Nourriture"", ""timestamp"": ""2026-01-29T15:01:59.700Z"" }},
          ""AND"",
          {{ ""type"": ""Event"", ""value"": ""{cameraTerm}"", ""timestamp"": ""2026-01-29T15:02:00.300Z"" }}
        ],
        ""limit"": ""-1""
      }}
    ]
  }}
]

## EXEMPLE 9: Commande de colorisation simple
Entrée utilisateur:
{{""Text"":""mets les pommes en bleu"",""Words"":[{{""Text"":""mets"",""StartedAt"":""2026-01-29T17:42:51.801Z"",""EndedAt"":""2026-01-29T17:42:52.051Z""}},{{""Text"":""les"",""StartedAt"":""2026-01-29T17:42:52.071Z"",""EndedAt"":""2026-01-29T17:42:52.211Z""}},{{""Text"":""pommes"",""StartedAt"":""2026-01-29T17:42:52.231Z"",""EndedAt"":""2026-01-29T17:42:52.601Z""}},{{""Text"":""en"",""StartedAt"":""2026-01-29T17:42:52.621Z"",""EndedAt"":""2026-01-29T17:42:52.751Z""}},{{""Text"":""bleu"",""StartedAt"":""2026-01-29T17:42:52.771Z"",""EndedAt"":""2026-01-29T17:42:53.101Z""}}]}}
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
--- FIN DES EXEMPLES ---
";
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);

            // Sérialise un objet anonyme qui correspond à la structure attendue par le prompt
            var userInput = new
            {
                sentence.Text,
                sentence.Words
            };

            var requestBody = new
            {
                model = "gpt-4-turbo",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = JsonConvert.SerializeObject(userInput) }
                },
                temperature = 0.1
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
            Debug.Log($"[LLM] Full API response body:\n{responseBody}");

            var openAiResponse = JsonConvert.DeserializeObject<OpenAiResponse>(responseBody);

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