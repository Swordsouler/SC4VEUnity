using NaughtyAttributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sc4ve.Multimodality.Intent;
using Sc4ve.Multimodality.Intent.RuleBased;
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
using UnityEngine.InputSystem;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace Sc4ve.Multimodality
{
    public enum LlmService
    {
        OpenAI,
        Local
    }

    public enum RecognizerMode
    {
        LLM,
        RuleBased
    }

    public class MultimodalityController : MonoBehaviour
    {
        [BoxGroup("References"), SerializeField] private BaseSpeechToText _speechToText;
        [BoxGroup("References"), SerializeField] private Language _language = Language.English;

        [BoxGroup("Feedback"), SerializeField,
         Tooltip("Énonce une confirmation vocale après chaque commande réussie (ex: « 6 objets coloriés »). Décocher pour la désactiver.")]
        private bool _voiceGrounding = true;

        [BoxGroup("Benchmark"), SerializeField,
         Tooltip("Pointage activé. Décocher pour l'ABLATION : déictiques + destinations ignorés → voix seule (mesure la contribution du pointage).")]
        private bool _pointingEnabled = true;

        [BoxGroup("Benchmark"), SerializeField,
         Tooltip("Journalise modalité / issue / durée par commande, et écrit un CSV (persistentDataPath/sven_metrics.csv).")]
        private bool _metricsEnabled = true;

        [BoxGroup("Recognizer Settings"), SerializeField, Tooltip("LLM : utilise un modèle de langage (OpenAI ou local). RuleBased : utilise uniquement des algorithmes, sans LLM.")]
        private RecognizerMode _recognizerMode = RecognizerMode.LLM;

        [BoxGroup("Recognizer Settings"), ShowIf("_recognizerMode", RecognizerMode.RuleBased), SerializeField,
         Tooltip("Délai (ms) ajouté après la fin de phrase pour le timestamp de destination d'un MoveCommand. " +
                 "Compense le fait que le pointeur n'est pas encore stabilisé au moment où 'ici'/'là' est prononcé.")]
        private int _movePointDelayMs = 300;

        [BoxGroup("LLM Settings"), ShowIf("IsLlmMode"), SerializeField]
        private LlmService _llmService = LlmService.OpenAI;

        [BoxGroup("LLM Settings"), ShowIf("IsLlmModeOpenAI"), SerializeField, Tooltip("Clé API OpenAI. Laisser vide pour utiliser la variable d'environnement OPENAI_API_KEY (recommandé : ce champ est sérialisé dans la scène, donc committé avec elle).")]
        private string _openAiApiKey;

        [BoxGroup("LLM Settings"), ShowIf("IsLlmModeLocal"), SerializeField, Tooltip("URL du serveur LLM local (ex: http://localhost:1234/v1).")]
        private string _localLlmUrl = "http://localhost:1234/v1";

        private bool IsLlmMode     => _recognizerMode == RecognizerMode.LLM;
        private bool IsLlmModeOpenAI => IsLlmMode && _llmService == LlmService.OpenAI;
        private bool IsLlmModeLocal  => IsLlmMode && _llmService == LlmService.Local;

        /// <summary>
        /// Clé API OpenAI effective : champ Inspector si renseigné, sinon variable
        /// d'environnement OPENAI_API_KEY. Préférer la variable d'environnement — le champ
        /// Inspector est sérialisé dans la scène, donc committé avec elle.
        /// </summary>
        private string OpenAiApiKey =>
            !string.IsNullOrWhiteSpace(_openAiApiKey)
                ? _openAiApiKey
                : Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };
        private RuleBasedIntentRecognizer _ruleBasedRecognizer;

        // Commande en attente d'une clarification (paramètre manquant) : la phrase suivante
        // qui fournit le paramètre la complète (« Colorie cette banane » → « En bleu »).
        private Command _pendingCommand;

        // Désambiguïsation en attente : référence au singulier (« la pomme ») qui correspond à
        // plusieurs cibles, sans pointage. L'énoncé suivant (l'utilisateur pointe une cible) la
        // résout par proximité au pointeur. Voir ResolveCommands / TryResolveDisambiguation.
        private class PendingDisambiguation
        {
            public Command Command;
            public List<SemantizationCore> Candidates;
        }
        private PendingDisambiguation _pendingDisambiguation;

        // Commandes d'agrégat/requête : agissent sur TOUTES les correspondances → pas de désambiguïsation.
        private static readonly HashSet<string> _noDisambiguation =
            new() { "CountCommand", "DescribeCommand", "MeasureCommand" };

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

--- FORMAT DE SORTIE ---
Ta réponse est UNIQUEMENT un tableau JSON. Structure exacte et obligatoire :
[
  {{
    ""type"": ""NomDeLaCommande"",
    ""parameters"": [
      {{ ""type"": ""SelectionParameter"", ""filters"": [ ... ], ""limit"": ""1"" }},
      {{ ""type"": ""PointParameter"",     ""value"": ""{pointerTerm}"", ""timestamp"": ""..."" }}
    ]
  }}
]
Erreurs de structure à ne JAMAIS commettre :
- La clé de commande est TOUJOURS ""type"" (jamais ""Command"", ""command"", ""name"" ou autre).
- Les paramètres sont TOUJOURS dans un tableau ""parameters"" ; chaque élément a sa propre clé ""type"".
- Ne jamais mettre les paramètres comme propriétés directes de l'objet commande.
- ""limit"" est TOUJOURS une chaîne : ""1"", ""-1"", ""3"" — JAMAIS un entier JSON.
- Pour PointParameter : ""value"" = nom du composant pointeur (ex: ""{pointerTerm}"") — JAMAIS le mot déictique (""ici"", ""là"", ""ça"", etc.).

--- ERREURS FRÉQUENTES À ÉVITER ---
1.  RÈGLE D'OR (COLORIZECOMMAND) : Pour une commande 'ColorizeCommand', la distinction entre couleur SOURCE et CIBLE est cruciale.
- La couleur CIBLE (ex: '... en rouge') va TOUJOURS et UNIQUEMENT dans le 'ColorParameter'.
- Une couleur SOURCE, qui décrit les objets à modifier (ex: 'les pommes vertes'), va dans un filtre 'Color' à l'intérieur du 'SelectionParameter'.
- Ne jamais mettre la couleur CIBLE dans un filtre 'Color' du 'SelectionParameter'.
2.  Pour une phrase comme 'colorie les légumes', NE PAS ajouter de filtre 'Event' pour '{pointerTerm}'. Il n'y a pas de mot déictique ('ce', 'cette', etc.), donc il n'y a pas de pointage.
3.  CORÉFÉRENCE EXCLUSIVE : Si la phrase contient UNIQUEMENT une commande suivie d'un pronom ('le', 'la', 'les', 'eux', 'celui-ci', etc.) sans description d'objet, c'est une coréférence. Le filtre 'Coreference' doit être SEUL dans la liste des filtres. AUCUN filtre 'Annotation' ne doit être ajouté.
4.  VOCABULAIRE STRICT : Les valeurs pour les filtres 'Annotation' et 'Color' DOIVENT provenir EXCLUSIVEMENT des listes de vocabulaire fournies. N'invente JAMAIS de termes. Si un mot comme 'objet' est utilisé par l'utilisateur mais ne figure pas dans le vocabulaire d'annotation, ne génère PAS de filtre 'Annotation' pour ce mot. Filtre uniquement sur les autres aspects décrits (comme la couleur, si applicable).
5.  STRUCTURE OBLIGATOIRE DU TABLEAU 'filters' : Le tableau 'filters' ne doit JAMAIS, en aucun cas, contenir deux objets de filtre JSON l'un après l'autre. Chaque objet de filtre DOIT être séparé du suivant par une chaîne de caractères : soit ""AND"", soit ""OR"". Si la logique de la phrase est une conjonction (ex: 'les voitures rouges'), utilise ""AND"". C'est le cas par défaut.
- **EXEMPLE INCORRECT** : `""filters"": [ {{ ""type"": ""Annotation"", ... }}, {{ ""type"": ""Color"", ... }} ]`
- **EXEMPLE CORRECT** : `""filters"": [ {{ ""type"": ""Annotation"", ... }}, ""AND"", {{ ""type"": ""Color"", ... }} ]`
- Omettre l'opérateur est une **erreur critique** qui rend le JSON invalide.
6.  PAS DE FILTRE D'ANNOTATION PAR DÉFAUT : Si la phrase de l'utilisateur est générale et ne spécifie pas de type d'objet (par exemple, 'tout', 'tout ce qui est...', 'les éléments'), ne génère PAS de filtre 'Annotation' par défaut. Si la phrase est 'colorie tout ce qui est bleu en rouge', le 'SelectionParameter' doit contenir UNIQUEMENT un filtre de type 'Color' pour la valeur 'Bleu', sans aucun filtre 'Annotation'.
7.  TIMESTAMPS OBLIGATOIRES : Chaque paramètre ou condition de filtre qui se rapporte à un mot ou un moment précis de la phrase DOIT IMPÉRATIVEMENT contenir une propriété ""timestamp"". La valeur doit correspondre à la propriété ""EndedAt"" du mot le plus pertinent, SAUF EXCEPTION.
- **Exception pour MoveCommand** : Pour un 'MoveCommand', le 'SelectionParameter' source (l'objet à déplacer) doit utiliser le 'StartedAt' du mot pertinent (ex: 'ça'). Le 'PointParameter' de destination (ex: 'ici') continue d'utiliser 'EndedAt'.
- S'applique à : 'Annotation', 'Color', 'Event', 'Coreference', 'PointParameter'.
- Par exemple, pour 'déplace ça ici', le 'SelectionParameter' (via son filtre 'Event' pour 'ça') aura un 'timestamp' basé sur le 'StartedAt' du mot 'ça', et le 'PointParameter' (pour 'ici') aura un 'timestamp' basé sur le 'EndedAt' du mot 'ici'.
8.  GESTION DES QUANTITÉS NUMÉRIQUES : Lorsque l'utilisateur spécifie une quantité explicite (ex: 'trois citrouilles', 'les 5 plus petites voitures'), tu DOIS utiliser cette quantité pour la propriété 'limit' du 'SelectionParameter'.
- Une quantité explicite (ex: 'trois', 'trois citrouilles') définit le nombre exact d'objets à sélectionner : `""limit"": ""3""`.
- Sans quantité explicite ou avec des quantificateurs généraux (ex: 'les', 'les citrouilles'), utilise : `""limit"": ""-1""` (tous les objets).
- La quantité s'applique UNIQUEMENT au 'SelectionParameter', JAMAIS au nombre de commandes générées (sauf pour l'enchaînement 'X fois').
9.  DÉICTIQUE vs CORÉFÉRENCE — RÈGLE ABSOLUE : Le mot 'ça' (et tout autre mot déictique) combiné avec un mot de destination (ici, là, là-bas, là-haut, dessus, etc.) est TOUJOURS un déictique. Utilise un filtre 'Event' avec le StartedAt de 'ça'. Ne génère JAMAIS un filtre 'Coreference' dans ce cas.
- 'ça' + destination spatiale → MoveCommand, filtre 'Event', timestamp = StartedAt de 'ça'.
- 'Coreference' uniquement si 'ça' / 'les' / 'eux' désigne des objets d'une commande précédente, SANS mot de destination spatiale.

--- COMMANDES DISPONIBLES ---
{availableCommandsString}

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

--- ENCHAÎNEMENT DE COMMANDES ---
Lorsque l'utilisateur demande d'effectuer une action plusieurs fois (ex: 'trois fois', 'deux fois', etc.), tu DOIS générer plusieurs commandes successives dans le tableau JSON principal.
- **Règle importante** : Chaque commande est un objet JSON complet et distinct dans le tableau de sortie.
- Le nombre de répétitions doit correspondre exactement au nombre demandé par l'utilisateur.
- Les paramètres doivent être répétés.
- **Distinction critique** : 'trois fois' (répète la même commande 3 fois) est DIFFÉRENT de 'trois citrouilles' (sélectionne 3 citrouilles dans une seule commande).

NOTE: Dans les exemples suivants, la propriété 'StartedAt' est généralement omise pour des raisons de concision, mais elle sera présente dans l'entrée utilisateur réelle. Elle est explicitement montrée dans les cas où elle est cruciale (ex: MoveCommand).

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
{{""Text"":""déplace ça ici"",""Words"":[{{""Text"":""déplace"",""StartedAt"":""2026-02-02T17:20:00.800Z"",""EndedAt"":""2026-02-02T17:20:01.000Z""}},{{""Text"":""ça"",""StartedAt"":""2026-02-02T17:20:01.100Z"",""EndedAt"":""2026-02-02T17:20:01.500Z""}},{{""Text"":""ici"",""StartedAt"":""2026-02-02T17:20:01.800Z"",""EndedAt"":""2026-02-02T17:20:02.000Z""}}]}}
JSON Attendu:
[
  {{
    ""type"": ""MoveCommand"",
    ""parameters"": [
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Event"", ""value"": ""{pointerTerm}"", ""timestamp"": ""2026-02-02T17:20:01.100Z"" }}
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

## EXEMPLE 12: Déplacement avec quantité numérique (QUANTITÉ DANS LA SÉLECTION)
Entrée utilisateur:
{{""Text"":""déplace trois citrouilles ici"",""Words"":[{{""Text"":""déplace"",""StartedAt"":""2026-02-02T17:20:00.800Z"",""EndedAt"":""2026-02-02T17:20:01.000Z""}},{{""Text"":""trois"",""EndedAt"":""2026-02-02T17:20:01.300Z""}},{{""Text"":""citrouilles"",""EndedAt"":""2026-02-02T17:20:01.800Z""}},{{""Text"":""ici"",""StartedAt"":""2026-02-02T17:20:02.000Z"",""EndedAt"":""2026-02-02T17:20:02.200Z""}}]}}
JSON Attendu:
[
  {{
    ""type"": ""MoveCommand"",
    ""parameters"": [
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Annotation"", ""value"": ""Citrouille"", ""timestamp"": ""2026-02-02T17:20:01.800Z"" }}
        ],
        ""limit"": ""3""
      }},
      {{
        ""type"": ""PointParameter"",
        ""value"": ""{pointerTerm}"",
        ""timestamp"": ""2026-02-02T17:20:02.200Z""
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

## EXEMPLE 14: Sélection avec ordre et limite
Entrée utilisateur:
{{""Text"":""sélectionne les 3 plus petites voitures"",""Words"":[]}}
JSON Attendu:
[
  {{
    ""type"": ""SelectCommand"",
    ""parameters"": [
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Annotation"", ""value"": ""Voiture"", ""timestamp"": ""..."" }}
        ],
        ""limit"": ""3"",
        ""order"": {{
          ""criterias"": [
            {{ ""type"": ""size"", ""desc"": false }}
          ]
        }}
      }}
    ]
  }}
]

## EXEMPLE 15: Agrandissement avec filtre OR
Entrée utilisateur:
{{""Text"":""agrandis les pommes ou les citrouilles"",""Words"":[]}}
JSON Attendu:
[
  {{
    ""type"": ""ScaleUpCommand"",
    ""parameters"": [
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Annotation"", ""value"": ""Pomme"", ""timestamp"": ""..."" }},
          ""OR"",
          {{ ""type"": ""Annotation"", ""value"": ""Citrouille"", ""timestamp"": ""..."" }}
        ],
        ""limit"": ""-1""
      }}
    ]
  }}
]

## EXEMPLE 16: Mesure de distance avec double déictique
Entrée utilisateur:
{{""Text"":""mesure la distance entre ça et ça"",""Words"":[]}}
JSON Attendu:
[
  {{
    ""type"": ""MeasureCommand"",
    ""parameters"": [
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Event"", ""value"": ""{pointerTerm}"", ""timestamp"": ""..."" }}
        ],
        ""limit"": ""1""
      }},
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Event"", ""value"": ""{pointerTerm}"", ""timestamp"": ""..."" }}
        ],
        ""limit"": ""1""
      }}
    ]
  }}
]

## EXEMPLE 17: Enchaînement de commandes (répétition multiple)
Entrée utilisateur:
{{""Text"":""assombris trois fois les légumes"",""Words"":[{{""Text"":""assombris"",""EndedAt"":""2026-03-05T14:20:01.000Z""}},{{""Text"":""trois"",""EndedAt"":""2026-03-05T14:20:01.500Z""}},{{""Text"":""fois"",""EndedAt"":""2026-03-05T14:20:01.800Z""}},{{""Text"":""les"",""EndedAt"":""2026-03-05T14:20:02.000Z""}},{{""Text"":""légumes"",""EndedAt"":""2026-03-05T14:20:02.500Z""}}]}}
JSON Attendu:
[
  {
    ""type"": ""ColorizeDarkerCommand"",
    ""parameters"": [
      {
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Annotation"", ""value"": ""Légume"", ""timestamp"": ""2026-03-05T14:20:02.500Z"" }}
        ],
        ""limit"": ""-1""
      }}
    ]
  },
  {
    ""type"": ""ColorizeDarkerCommand"",
    ""parameters"": [
      {
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Annotation"", ""value"": ""Légume"", ""timestamp"": ""2026-03-05T14:20:02.500Z"" }}
        ],
        ""limit"": ""-1""
      }}
    ]
  },
  {
    ""type"": ""ColorizeDarkerCommand"",
    ""parameters"": [
      {
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Annotation"", ""value"": ""Légume"", ""timestamp"": ""2026-03-05T14:20:02.500Z"" }}
        ],
        ""limit"": ""-1""
      }}
    ]
  }
]
--- FIN DES EXEMPLES ---
";

        [BoxGroup("LLM Settings"), ShowIf("IsLlmModeOpenAI"), SerializeField,
         Tooltip("Modèle rapide pour les requêtes simples. gpt-4o-mini est recommandé.")]
        private string _fastModel = "gpt-4o-mini";

        [BoxGroup("LLM Settings"), ShowIf("IsLlmModeOpenAI"), SerializeField,
         Tooltip("Modèle précis utilisé en fallback si la validation échoue. gpt-4o est recommandé.")]
        private string _preciseModel = "gpt-4o";

        private Task _initializationTask;
        private string _annotationTypesString;
        private string _availableColorsString;
        private string _cameraNamesString;
        private string _pointerNamesString;
        private string _pointerDeicticsString;
        private string _availableCommandsString;
        // Prompt système compilé une seule fois après InitializeVocabularies.
        // Évite de reconstruire la chaîne à chaque appel LLM et permet à OpenAI
        // de mettre le prompt en cache (économie ~50 % du coût des tokens prompt).
        private string _cachedSystemPrompt;
        // Version allégée sans la section EXEMPLES (~3 500 tokens en moins).
        // Utilisée pour les serveurs locaux dont le n_ctx est limité (4 096 par défaut).
        private string _cachedSystemPromptLocal;

        private PiperTextToSpeech _tts;
        private Action _ttsSpeechStartHandler;
        private Action _ttsSpeechEndHandler;

        private void Awake()
        {
            UserData.Language = _language;
            if (_speechToText != null) _speechToText.OnTranscriptionResult += OnTranscriptionResult;

            // Suspendre l'écoute pendant que le système parle (Piper) : sinon le micro re-capte
            // la voix de synthèse et la réinterprète comme une commande (boucle de rétroaction).
            _tts = FindAnyObjectByType<PiperTextToSpeech>();
            if (_tts != null && _speechToText != null)
            {
                _ttsSpeechStartHandler = () => _speechToText.SetListeningSuspended(true);
                _ttsSpeechEndHandler   = () => _speechToText.SetListeningSuspended(false);
                _tts.OnSpeechStart += _ttsSpeechStartHandler;
                _tts.OnSpeechEnd   += _ttsSpeechEndHandler;
            }
        }

        private void OnDestroy()
        {
            // Désabonnements symétriques d'Awake : sans eux, STT/TTS continueraient d'invoquer
            // un contrôleur détruit après un rechargement de scène (MissingReferenceException).
            if (_speechToText != null) _speechToText.OnTranscriptionResult -= OnTranscriptionResult;
            if (_tts != null)
            {
                if (_ttsSpeechStartHandler != null) _tts.OnSpeechStart -= _ttsSpeechStartHandler;
                if (_ttsSpeechEndHandler != null) _tts.OnSpeechEnd -= _ttsSpeechEndHandler;
            }
        }

        private async void OnTranscriptionResult(string obj)
        {
            var result = new RecognitionResult(obj, _speechToText.RecognizerStartedAt);
            if (result.Phrases.Any(p => !string.IsNullOrWhiteSpace(p.Text)))
                Debug.Log($"[LLM] Received transcription result: {obj}");
            for (int i = 0; i < result.Phrases.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(result.Phrases[i].Text)) continue;

                Sentence phrase = result.Phrases[i];
                phrase.Start(new Instant(phrase.StartedAt));
                phrase.End(new Instant(phrase.EndedAt));

                // Benchmark : applique l'ablation (pointage on/off) et démarre la mesure de l'énoncé.
                MultimodalitySettings.PointingEnabled = _pointingEnabled;
                MultimodalityMetrics.Enabled = _metricsEnabled;
                MultimodalityMetrics.Begin(phrase.Text);

                try
                {
                    // Réponse à une désambiguïsation en attente : l'utilisateur a désigné une cible.
                    if (_pendingDisambiguation != null)
                    {
                        if (TryResolveDisambiguation(phrase)) continue;
                        _pendingDisambiguation = null; // pas une réponse de pointage → on abandonne
                    }

                    string commandJson;

                    if (_recognizerMode == RecognizerMode.RuleBased)
                    {
                        Debug.Log($"[RuleBased] Analyse de la phrase : \"{phrase.Text}\"");
                        await InitializeVocabulariesAsync();
                        EnsureRuleBasedRecognizer();
                        commandJson = _ruleBasedRecognizer.Recognize(phrase);
                        if (string.IsNullOrWhiteSpace(commandJson))
                        {
                            // Réponse à une clarification en attente (ex: « en bleu » après « colorie cette banane »).
                            if (_pendingCommand != null)
                                commandJson = _ruleBasedRecognizer.CompletePending(phrase, _pendingCommand);

                            if (string.IsNullOrWhiteSpace(commandJson))
                            {
                                Debug.LogWarning("[RuleBased] Aucune commande produite pour cette phrase.");
                                // Paramètre isolé sans commande (« en vert ») → ambiguïté : on nomme
                                // les actions possibles plutôt qu'un « je n'ai pas compris » générique.
                                string orphanParam = _ruleBasedRecognizer.DetectOrphanParameter(phrase);
                                string orphanPrompt = ClarificationVocabulary.GetOrphanPrompt(orphanParam);
                                Command.Speak(orphanPrompt ?? ClarificationVocabulary.NotUnderstood);
                                MultimodalityMetrics.Complete(null, orphanParam != null ? "orphan" : "not_understood", 0);
                                _pendingCommand = null;
                                continue;
                            }
                        }
                    }
                    else
                    {
                        Debug.Log($"[LLM] Sending sentence for analysis: \"{phrase.Text}\"");
                        commandJson = await GetValidatedCommandJsonFromLlmAsync(phrase);
                        if (string.IsNullOrWhiteSpace(commandJson))
                        {
                            Debug.LogWarning("[LLM] Received empty or null JSON from LLM after all attempts.");
                            Command.Speak(ClarificationVocabulary.NotUnderstood); // manque de sens
                            MultimodalityMetrics.Complete(null, "not_understood", 0);
                            continue;
                        }
                        Debug.Log($"[LLM] Received FINAL JSON: {commandJson}");
                    }

                    List<Command> commands = DeserializeCommand(commandJson);
                    if (commands == null || commands.Count == 0 || commands.Any(c => c is UnknownCommand))
                    {
                        Command.Speak(ClarificationVocabulary.NotUnderstood); // manque de sens
                        MultimodalityMetrics.Complete(commands?.FirstOrDefault(), "not_understood", 0);
                        continue;
                    }

                    await CommandToGraphOutputCommandAsync(commands);
                    ResolveCommands(commands);
                    Debug.Log($"[{_recognizerMode}] Commands resolved successfully.");
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    Debug.LogError($"[{_recognizerMode}] An error occurred during processing.");
                    Debug.LogException(e);
                    MultimodalityMetrics.Complete(null, "error", 0);
                }
            }
        }

        /// <summary>
        /// Orchestrateur de l'approche hybride. Tente une requête rapide avec GPT-3.5,
        /// la valide, et ne passe à GPT-4 qu'en cas d'erreur connue.
        /// Pour un LLM local, une seule requête est effectuée.
        /// </summary>
        private async Task<string> GetValidatedCommandJsonFromLlmAsync(Sentence sentence)
        {
            if (_llmService == LlmService.Local)
            {
                Debug.Log("[LLM] Using local LLM...");
                string jsonResponse = await CallLlmApiAsync(sentence, "local-model");
                if (string.IsNullOrWhiteSpace(jsonResponse))
                {
                    Debug.LogError("[LLM] Local LLM returned an empty response.");
                    return null;
                }
                return jsonResponse;
            }

            // --- Chemin OpenAI avec validation et bascule ---
            // 1. Essai rapide avec le modèle léger
            Debug.Log($"[LLM] Attempting fast path with {_fastModel}...");
            string fastResponse = await CallLlmApiAsync(sentence, _fastModel);

            if (string.IsNullOrWhiteSpace(fastResponse))
            {
                Debug.LogError($"[LLM] {_fastModel} returned an empty response. No fallback will be attempted.");
                return null;
            }

            // 2. Validation de la réponse
            List<Command> commands = DeserializeCommand(fastResponse);
            if (commands == null)
            {
                Debug.LogWarning($"[LLM] Failed to deserialize {_fastModel} response. Retrying with {_preciseModel}.");
                return await CallLlmApiAsync(sentence, _preciseModel);
            }

            bool needsCorrection = false;

            // Règle 0 : liste vide ou type de commande inconnu (halluciné par le modèle léger).
            // Le modèle précis a une chance de produire le bon type — l'essayer avant d'abandonner.
            if (commands.Count == 0 || commands.Any(c => c is UnknownCommand))
            {
                Debug.Log("[LLM] Validation failed (R0): empty command list or unknown command type.");
                needsCorrection = true;
            }

            // Règle 1 : ColorizeCommand ne doit pas contenir de filtre Color dans le SelectionParameter
            // (la couleur cible va dans ColorParameter, pas dans les filtres de sélection).
            if (!needsCorrection && commands.Any(c => c is ColorizeCommand &&
                (c.Parameters?.OfType<SelectionParameter>().FirstOrDefault()
                    ?.Filters.Any(f => !f.IsOperator && f.Condition?.Type == "Color") ?? false)))
            {
                Debug.Log("[LLM] Validation failed (R1): ColorizeCommand contains a 'Color' filter in SelectionParameter.");
                needsCorrection = true;
            }

            // Règle 2 : Si un déictique est présent, au moins un SelectionParameter doit avoir un filtre Event.
            if (!needsCorrection)
            {
                HashSet<string> deicticWords = new(
                    _pointerDeicticsString.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(s => s.Trim('\'').ToLower()));
                bool sentenceHasDeictic = deicticWords.Any(w => sentence.Text.ToLower().Contains(w));

                if (sentenceHasDeictic)
                {
                    var allSelectionParams = commands.SelectMany(c => c.Parameters?.OfType<SelectionParameter>()
                                                                       ?? Enumerable.Empty<SelectionParameter>());
                    if (allSelectionParams.Any() &&
                        allSelectionParams.All(sp => !sp.Filters.Any(f => !f.IsOperator && f.Condition?.Type == "Event")))
                    {
                        Debug.Log("[LLM] Validation failed (R2): Deictic word found but no Event filter in any SelectionParameter.");
                        needsCorrection = true;
                    }
                }
            }

            // Règle 3 : Deux conditions adjacentes sans opérateur AND/OR entre elles.
            // C'est l'erreur la plus fréquente des modèles légers sur ce format.
            if (!needsCorrection)
            {
                foreach (Command cmd in commands)
                {
                    foreach (SelectionParameter sp in cmd.Parameters?.OfType<SelectionParameter>()
                                                        ?? Enumerable.Empty<SelectionParameter>())
                    {
                        if (sp.Filters == null) continue;
                        for (int i = 0; i < sp.Filters.Count - 1; i++)
                        {
                            if (!sp.Filters[i].IsOperator && !sp.Filters[i + 1].IsOperator)
                            {
                                Debug.Log($"[LLM] Validation failed (R3): consecutive filter conditions at index {i} without AND/OR operator.");
                                needsCorrection = true;
                                break;
                            }
                        }
                        if (needsCorrection) break;
                    }
                    if (needsCorrection) break;
                }
            }

            // 3. Si la validation échoue, on corrige avec le modèle précis
            if (needsCorrection)
            {
                Debug.LogWarning($"[LLM] {_fastModel} response failed validation. Retrying with {_preciseModel}.");
                return await CallLlmApiAsync(sentence, _preciseModel);
            }

            Debug.Log($"[LLM] {_fastModel} response passed validation. Using fast path result.");
            return fastResponse;
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
            var pointerNameTask = Sven.Context.Pointer.GetAllAvailableNames(UserData.Locale);
            var cameraNameTask = PointOfView.GetAllAvailableNames(UserData.Locale);

            await Task.WhenAll(annotationTypesTask, availableColorsTask, pointerNameTask, cameraNameTask);

            List<string> annotationTypes = await annotationTypesTask;
            _annotationTypesString = string.Join(", ", annotationTypes.Select(t => $"{t}"));

            List<string> availableColors = await availableColorsTask;
            _availableColorsString = string.Join(", ", availableColors.Select(c => $"{c}"));

            List<string> pointerNames = await pointerNameTask;
            _pointerNamesString = string.Join(", ", pointerNames.Select(n => $"{n}"));

            List<string> cameraNames = await cameraNameTask;
            _cameraNamesString = string.Join(", ", cameraNames.Select(n => $"{n}"));

            // Vocabulaire de commandes (triggers + descriptions LLM), bilingue, depuis l'ontologie
            // (repli sur les attributs C# pour les commandes pas encore migrées).
            await CommandVocabulary.InitializeAsync();
            _availableCommandsString = CommandVocabulary.CommandsDescription;
            // Déictiques de pointage (sc4ve:deicticWord), bilingues, depuis l'ontologie.
            _pointerDeicticsString = string.Join(", ", CommandVocabulary.Deictics);

            // Exigences de paramètres + messages de clarification (bilingues) depuis l'ontologie.
            await ClarificationVocabulary.InitializeAsync();

            // Compilation du prompt système définitif (fait une seule fois par session).
            // Le résultat est identique entre tous les appels → OpenAI peut le mettre en
            // cache côté serveur (prompt caching automatique pour les prompts > 1024 tokens).
            _cachedSystemPrompt = SYSTEM_PROMPT_TEMPLATE
                // Les exemples JSON du template utilisent des accolades doublées ({{ }}),
                // vestige d'un ancien usage de string.Format. On les normalise en accolades
                // simples (JSON valide) AVANT d'injecter les vocabulaires, pour ne jamais
                // altérer les valeurs substituées (qui ne contiennent pas d'accolades).
                .Replace("{{", "{")
                .Replace("}}", "}")
                .Replace("{annotationTypesString}", _annotationTypesString)
                .Replace("{availableColorsString}", _availableColorsString)
                .Replace("{cameraTerm}", _cameraNamesString)
                .Replace("{pointerTerm}", _pointerNamesString)
                .Replace("{pointerDeicticsString}", _pointerDeicticsString)
                .Replace("{availableCommandsString}", _availableCommandsString);

            // Version locale : on retire la section EXEMPLES pour réduire la taille du prompt
            // (~6 500 → ~3 000 tokens) et permettre de fonctionner avec n_ctx = 4 096.
            _cachedSystemPromptLocal = TrimExamplesSection(_cachedSystemPrompt);

            Debug.Log($"[LLM] Vocabularies cached. Prompt: {_cachedSystemPrompt.Length} chars (full), " +
                      $"{_cachedSystemPromptLocal.Length} chars (local/no-examples).");
        }

        /// <summary>
        /// Crée le RuleBasedIntentRecognizer en utilisant les vocabulaires déjà chargés,
        /// puis injecte ce vocabulaire dans Vosk comme grammaire pour restreindre la
        /// reconnaissance aux mots du domaine (évite les fusions phonétiques).
        /// Doit être appelé après InitializeVocabulariesAsync().
        /// </summary>
        private void EnsureRuleBasedRecognizer()
        {
            if (_ruleBasedRecognizer != null) return;

            List<string> annotationTypes = _annotationTypesString
                .Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            List<string> availableColors = _availableColorsString
                .Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            List<string> pointerDeictics = _pointerDeicticsString
                .Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim('\''))
                .ToList();

            _ruleBasedRecognizer = new RuleBasedIntentRecognizer(
                annotationTypes,
                availableColors,
                pointerDeictics,
                _pointerNamesString,
                _cameraNamesString,
                _movePointDelayMs);

            Debug.Log($"[RuleBased] Reconnaisseur initialisé — {annotationTypes.Count} annotations, " +
                      $"{availableColors.Count} couleurs, {pointerDeictics.Count} déictiques.");

            // Injecter le vocabulaire du domaine dans Vosk pour améliorer la précision STT.
            if (_speechToText != null)
                _speechToText.SetGrammar(BuildVoskGrammar(annotationTypes, availableColors, pointerDeictics));
        }

        /// <summary>
        /// Construit la liste de mots à fournir à Vosk comme vocabulaire de reconnaissance.
        /// Inclut : verbes d'action, annotations, couleurs, déictiques, mots fonctionnels français.
        /// Tous les mots sont en minuscules (exigence Vosk).
        /// </summary>
        private static List<string> BuildVoskGrammar(
            List<string> annotationTypes,
            List<string> availableColors,
            List<string> pointerDeictics)
        {
            var vocab = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // ── Verbes d'action (toutes les formes des triggers) ──────────
            foreach (var (triggers, _) in CommandVocabulary.TriggerMappings)
                foreach (string trigger in triggers)
                    foreach (string word in trigger.ToLowerInvariant().Split(' '))
                        vocab.Add(word);

            // ── Annotations et couleurs (depuis l'ontologie) ──────────────
            foreach (string a in annotationTypes) vocab.Add(a.ToLowerInvariant());
            foreach (string c in availableColors)  vocab.Add(c.ToLowerInvariant());

            // ── Déictiques ────────────────────────────────────────────────
            foreach (string d in pointerDeictics)  vocab.Add(d.ToLowerInvariant());

            // ── Mots fonctionnels courants, par langue ────────────────────
            // FR : "mais" volontairement absent (homophone de "mets" /mɛ/) → Vosk/Whisper
            // choisit "mets" pour ce son.
            string[] functionWords = (UserData.Locale ?? "fr").StartsWith("en")
                ? new[]
                {
                    "the", "a", "an", "some", "of", "to", "on", "in", "at",
                    "and", "or",
                    "here", "there", "this", "that", "these", "those", "it", "them",
                    "more", "less", "very", "all", "every",
                    "one", "two", "three", "four", "five",
                    "six", "seven", "eight", "nine", "ten"
                }
                : new[]
                {
                    "le", "la", "les", "l", "un", "une", "des",
                    "de", "du", "en", "à", "au", "aux",
                    "et", "ou", "donc",
                    "ici", "là", "là-bas", "là-haut",
                    "ce", "cet", "cette", "ces",
                    "ça", "cela", "ceci",
                    "plus", "moins", "très",
                    "tous", "toutes", "tout", "toute",
                    "il", "elle", "ils", "elles",
                    "un", "deux", "trois", "quatre", "cinq",
                    "six", "sept", "huit", "neuf", "dix"
                };
            foreach (string w in functionWords)
                vocab.Add(w);

            var result = vocab.OrderBy(w => w).ToList();
            Debug.Log($"[Vosk] Grammaire construite : {result.Count} mots.");
            return result;
        }

        /// <summary>
        /// Appelle l'API LLM (OpenAI ou locale) avec le modèle et la phrase spécifiés.
        /// </summary>
        private async Task<string> CallLlmApiAsync(Sentence sentence, string model)
        {
            await InitializeVocabulariesAsync();

            string finalSystemPrompt = _llmService == LlmService.Local
                ? _cachedSystemPromptLocal
                : _cachedSystemPrompt;

            var userInput = new { sentence.Text, sentence.Words };
            var requestObject = new JObject
            {
                ["model"]       = model,
                ["messages"]    = new JArray(
                    new JObject { ["role"] = "system", ["content"] = finalSystemPrompt },
                    new JObject { ["role"] = "user",   ["content"] = JsonConvert.SerializeObject(userInput) }
                ),
                ["temperature"] = 0.0,
                ["max_tokens"]  = 2048
            };
            // json_object est supporté par OpenAI gpt-4o/mini mais pas par tous les serveurs locaux.
            // On l'omet côté local pour éviter les erreurs de compatibilité ;
            // StripMarkdownJson() gère le cas où le modèle emballe quand même en markdown.
            if (_llmService == LlmService.OpenAI)
                requestObject["response_format"] = new JObject { ["type"] = "json_object" };
            Debug.Log(JsonConvert.SerializeObject(userInput) + "\n\n" + finalSystemPrompt);

            HttpRequestMessage requestMessage;
            string endpointUrlForLogging;

            if (_llmService == LlmService.OpenAI)
            {
                string apiKey = OpenAiApiKey;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    Debug.LogError("[LLM] Clé API OpenAI absente : définir la variable d'environnement OPENAI_API_KEY (recommandé) ou le champ Inspector.");
                    return null;
                }
                const string openAiUrl = "https://api.openai.com/v1/chat/completions";
                requestMessage = new HttpRequestMessage(HttpMethod.Post, openAiUrl);
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                endpointUrlForLogging = openAiUrl;
            }
            else // LlmService.Local
            {
                if (string.IsNullOrWhiteSpace(_localLlmUrl))
                {
                    Debug.LogError("[LLM] Local LLM URL is not set. Please set it in the inspector.");
                    return null;
                }
                string localApiUrl = _localLlmUrl.TrimEnd('/') + "/chat/completions";
                requestMessage = new HttpRequestMessage(HttpMethod.Post, localApiUrl);
                endpointUrlForLogging = localApiUrl;
            }

            requestMessage.Content = new StringContent(JsonConvert.SerializeObject(requestObject), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.SendAsync(requestMessage);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    Debug.LogError($"[LLM] API Error ({model} @ {endpointUrlForLogging}): {response.StatusCode}\n{errorBody}");
                    return null;
                }

                string responseBody = await response.Content.ReadAsStringAsync();
                var openAiResponse = JsonConvert.DeserializeObject<OpenAiResponse>(responseBody);

                if (openAiResponse?.Usage != null)
                {
                    var usage = openAiResponse.Usage;
                    Debug.Log($"[LLM] Token Usage ({model}): Prompt={usage.PromptTokens}, Completion={usage.CompletionTokens}, Total={usage.TotalTokens}");
                }

                if (openAiResponse?.Choices == null || openAiResponse.Choices.Count == 0)
                {
                    // Un « choices »: [] arrive (filtre de contenu, certains serveurs locaux) :
                    // sans cette garde, l'indexation lève et court-circuite le repli gracieux.
                    Debug.LogError($"[LLM] Réponse sans 'choices' exploitable ({model} @ {endpointUrlForLogging}) : {responseBody}");
                    return null;
                }
                return StripMarkdownJson(openAiResponse.Choices[0]?.Message?.Content);
            }
            catch (HttpRequestException e)
            {
                Debug.LogError($"[LLM] Network Error when calling {endpointUrlForLogging}: {e.Message}");
                return null;
            }
            catch (TaskCanceledException)
            {
                // Le timeout HttpClient est surfacé en TaskCanceledException, pas en
                // HttpRequestException : sans ce catch, il remonte comme erreur générique.
                Debug.LogError($"[LLM] Timeout après {_httpClient.Timeout.TotalSeconds:0} s en appelant {endpointUrlForLogging}.");
                return null;
            }
        }

        /// <summary>
        /// Remplace la section EXEMPLES complète par le seul EXEMPLE 11 (MoveCommand déictique).
        /// EXEMPLE 11 illustre directement la règle 9 : "ça" → filtre Event + StartedAt.
        /// Réduit le prompt de ~6 500 à ~3 200 tokens pour les serveurs locaux à n_ctx limité.
        /// </summary>
        private static string TrimExamplesSection(string prompt)
        {
            const string examplesMarker = "--- EXEMPLES ---";
            int startIdx = prompt.IndexOf(examplesMarker, StringComparison.Ordinal);
            if (startIdx < 0) return prompt;

            // Supprimer le paragraphe "NOTE:" qui décrit les raccourcis propres aux exemples
            string before = prompt[..startIdx];
            const string notePrefix = "\nNOTE:";
            int noteIdx = before.LastIndexOf(notePrefix, StringComparison.Ordinal);
            if (noteIdx >= 0)
                before = before[..noteIdx];

            // Conserver uniquement EXEMPLE 11 (MoveCommand avec déictique 'ça' + 'ici').
            // C'est l'exemple le plus critique : sans lui, les petits modèles confondent
            // 'ça' déictique (→ filtre Event, StartedAt) avec 'ça' coréférentiel.
            const string ex11Marker = "## EXEMPLE 11:";
            const string ex12Marker = "## EXEMPLE 12:";
            int ex11Idx = prompt.IndexOf(ex11Marker, StringComparison.Ordinal);
            int ex12Idx = prompt.IndexOf(ex12Marker, StringComparison.Ordinal);

            if (ex11Idx > 0 && ex12Idx > ex11Idx)
            {
                string example11 = prompt[ex11Idx..ex12Idx].TrimEnd();
                return before.TrimEnd() + "\n\n--- EXEMPLE DE RÉFÉRENCE ---\n" + example11 + "\n";
            }

            return before.TrimEnd() + "\n";
        }

        /// <summary>
        /// Retire les balises markdown (```json … ```) qu'OpenAI insère parfois autour du JSON.
        /// </summary>
        private static string StripMarkdownJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var match = System.Text.RegularExpressions.Regex.Match(
                text, @"```(?:json)?\s*([\s\S]*?)```",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : text.Trim();
        }

        private List<Command> DeserializeCommand(string json)
        {
            try
            {
                // response_format=json_object pousse certains modèles à envelopper le tableau
                // dans un objet ({"commands": [...]}) ou à renvoyer une commande seule : on
                // accepte ces variantes plutôt que d'escalader inutilement vers le modèle précis.
                List<Command> commands = null;
                string trimmed = json?.TrimStart();
                if (!string.IsNullOrEmpty(trimmed) && trimmed.StartsWith("{"))
                {
                    JObject wrapper = JObject.Parse(json);
                    JToken array = wrapper.Properties().Select(p => p.Value)
                                          .FirstOrDefault(v => v.Type == JTokenType.Array);
                    if (array != null) commands = array.ToObject<List<Command>>();
                    else if (wrapper["type"] != null) commands = new List<Command> { wrapper.ToObject<Command>() };
                }
                commands ??= JsonConvert.DeserializeObject<List<Command>>(json);
                // Le tableau peut contenir des éléments null ([null, {…}]) : on les retire,
                // les validations et ResolveCommands supposent des éléments non nuls.
                return commands?.Where(c => c != null).ToList();
            }
            catch (Exception e)
            {
                Debug.LogError($"[LLM] JSON Deserialization failed: {e.Message}\nJSON was: {json}");
                return null;
            }
        }

        public async Task<List<Command>> CommandToGraphOutputCommandAsync(List<Command> commands)
        {
            // Sur le thread principal (pas de Task.Run) : Semanticize interroge la copie du
            // graphe de scène et GraphManager.Assert mute le graphe partagé — aucun des deux
            // n'est thread-safe vis-à-vis du code Unity qui les utilise en parallèle.
            // Copie du graphe ontologique mis en cache (parsé une seule fois) au lieu de
            // re-parser tous les .ttl à chaque commande — Merge copie triples + namespaces.
            Graph cached = await OntologyCache.GetGraphAsync();
            Graph graph = new();
            graph.Merge(cached);
            graph.BaseUri = new Uri(SvenSettings.BaseUri);
            graph.NamespaceMap.AddNamespace("", UriFactory.Create(SvenSettings.BaseUri));
            foreach (Command command in commands)
                await command.Semanticize(graph);

            GraphManager.Assert(graph.Triples);
            return commands;
        }

        public void ResolveCommands(List<Command> commands)
        {
            // Résolution vide : la cible est spécifiée (filtres) mais aucun objet ne correspond
            // (ex: « colorie cette pomme » sans pomme pointée). On l'annonce sans exécuter, et
            // avant la clarification : inutile de demander la couleur s'il n'y a pas de cible.
            foreach (Command command in commands)
            {
                string noMatch = ClarificationVocabulary.GetNoMatchPrompt(command);
                if (noMatch != null)
                {
                    Debug.Log($"[NoMatch] {command.Type} → « {noMatch} »");
                    Command.Speak(noMatch);
                    MultimodalityMetrics.Complete(command, "no_match", 0);
                    _pendingCommand = null;
                    return;
                }
            }

            // Manque de paramètre : un paramètre requis manque (d'après les restrictions OWL).
            // On pose la question (bilingue, depuis le graphe), on sélectionne la cible déjà
            // résolue et on mémorise la commande pour la compléter à la phrase suivante.
            foreach (Command command in commands)
            {
                string clarification = ClarificationVocabulary.GetMissingParameterPrompt(command);
                if (clarification != null)
                {
                    List<SemantizationCore> target = command.Parameters?
                        .OfType<SelectionParameter>().FirstOrDefault()?.Objects;
                    if (target != null && target.Count > 0)
                    {
                        Command.LastObjects = target;
                        SelectionManager.SetSelection(target);
                    }
                    _pendingCommand = command;
                    Debug.Log($"[Clarification] {command.Type} : paramètre manquant → « {clarification} »");
                    Command.Speak(clarification);
                    MultimodalityMetrics.Complete(command, "clarification", 0);
                    return;
                }
            }

            // Désambiguïsation : référence au singulier (« la pomme ») correspondant à PLUSIEURS
            // objets, sans pointage / coréférence / repli-sélection → on demande laquelle et on met
            // en attente ; l'énoncé suivant (l'utilisateur pointe une cible) la résout au pointeur.
            foreach (Command command in commands)
            {
                if (_noDisambiguation.Contains(command.Type)) continue;
                SelectionParameter sel = command.Parameters?.OfType<SelectionParameter>().FirstOrDefault();
                if (sel == null || !sel.SingularIntent || sel.FallbackToSelection) continue;
                List<SemantizationCore> objs = sel.Objects;
                if (objs == null || objs.Count <= 1) continue;
                bool pointingOrCoref = sel.Filters != null && sel.Filters.Any(f =>
                    !f.IsOperator && f.Condition != null && (f.Condition.IsEvent || f.Condition.IsCoreference));
                if (pointingOrCoref) continue;

                _pendingDisambiguation = new PendingDisambiguation { Command = command, Candidates = objs };
                _pendingCommand = null;
                SelectionManager.SetSelection(objs);   // surbrillance des candidats
                string prompt = ClarificationVocabulary.GetDisambiguationPrompt(objs.Count);
                Debug.Log($"[Disambiguation] {command.Type} : {objs.Count} cibles pour une référence au singulier.");
                if (!string.IsNullOrEmpty(prompt)) Command.Speak(prompt);
                MultimodalityMetrics.Complete(command, "disambiguation", objs.Count);
                return;
            }

            _pendingCommand = null; // commande complète → plus rien en attente

            int undoBefore = CommandHistory.UndoCount;

            List<SemantizationCore> lastObjects = new();
            foreach (Command command in commands)
            {
                List<SemantizationCore> affected = command.Execute();
                lastObjects.AddRange(affected);
                // Grounding vocal : confirme ce qui a été fait (« 6 objets coloriés »), si activé.
                if (_voiceGrounding)
                {
                    string grounding = CommandVocabulary.GetGrounding(command.Type, affected?.Count ?? 0);
                    if (!string.IsNullOrEmpty(grounding)) Command.Speak(grounding);
                }
            }
            Command.LastObjects = lastObjects;
            MultimodalityMetrics.Complete(commands.FirstOrDefault(), "executed", lastObjects.Count);

            // La sélection (et son contour) suit toujours les objets de la dernière commande.
            List<SemantizationCore> selection = Command.LastObjects;

            // Si cette phrase a produit une action annulable, on mémorise les objets affectés
            // pour pouvoir les re-sélectionner lors d'un undo/redo.
            if (CommandHistory.UndoCount > undoBefore)
                CommandHistory.SetLastAffected(selection);

            SelectionManager.SetSelection(selection);
        }

        /// <summary>
        /// Résout une désambiguïsation en attente. Si l'énoncé est une vraie commande, retourne
        /// false (on abandonne, traitement normal). Sinon (l'utilisateur a désigné une cible au
        /// pointeur), exécute la commande en attente sur le candidat le plus proche du pointeur.
        /// </summary>
        private bool TryResolveDisambiguation(Sentence phrase)
        {
            // Une vraie commande dans la réponse → ce n'est pas une désambiguïsation.
            if (_ruleBasedRecognizer != null)
            {
                if (!string.IsNullOrWhiteSpace(_ruleBasedRecognizer.Recognize(phrase)))
                    return false;
            }
            // Mode LLM : pas de reconnaisseur RuleBased disponible — on détecte une nouvelle
            // commande via les déclencheurs du vocabulaire, pour ne pas consommer « annule » /
            // « colorie… » comme une désignation de pointage.
            else if (ContainsCommandTrigger(phrase.Text))
                return false;

            PendingDisambiguation pending = _pendingDisambiguation;
            _pendingDisambiguation = null;

            SemantizationCore chosen = ClosestCandidateToPointer(pending.Candidates);
            if (chosen == null)
            {
                Command.Speak(ClarificationVocabulary.NotUnderstood);
                MultimodalityMetrics.Complete(pending.Command, "not_understood", 0);
                return true;
            }

            SelectionParameter sel = pending.Command.Parameters?.OfType<SelectionParameter>().FirstOrDefault();
            if (sel != null) sel.ObjectsUri = new List<string> { chosen.GetUUID() };
            Debug.Log($"[Disambiguation] Cible désignée : {chosen.GetUUID()} (parmi {pending.Candidates.Count}).");
            ResolveCommands(new List<Command> { pending.Command });
            return true;
        }

        /// <summary>Vrai si le texte contient un déclencheur de commande connu (mot entier).</summary>
        private static bool ContainsCommandTrigger(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (var (triggers, _) in CommandVocabulary.TriggerMappings)
                foreach (string trigger in triggers)
                    if (System.Text.RegularExpressions.Regex.IsMatch(
                            text, $@"\b{System.Text.RegularExpressions.Regex.Escape(trigger)}\b",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        return true;
            return false;
        }

        /// <summary>Le candidat le plus proche de la position pointée par le pointeur, ou null.</summary>
        private static SemantizationCore ClosestCandidateToPointer(List<SemantizationCore> candidates)
        {
            Pointer pointer = UnityEngine.Object.FindAnyObjectByType<Pointer>();
            if (pointer == null || candidates == null) return null;
            Vector3 hit = pointer.PointerHitPosition;
            return candidates.Where(c => c != null)
                             .OrderBy(c => Vector3.Distance(c.transform.position, hit))
                             .FirstOrDefault();
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

        public void PrintTest()
        {
            Debug.Log(JsonConvert.SerializeObject(new Sentence("Colorie en rouge les cinq plus grosses citrouilles ou pomme que je vois")));
        }

        private void Update()
        {
            HandlePointerDown();
            // Fire-and-forget délibéré : HandlePointerUp gère toutes ses exceptions en interne.
            _ = HandlePointerUp();
        }

        private Parameter thisParameter = null;
        private Parameter thereParameter = null;
        private bool _isResolvingCommand = false;

        public void HandlePointerDown()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
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

        public async Task HandlePointerUp()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                thereParameter = new PointParameter
                {
                    Value = "Pointeur",
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
                try
                {
                    await CommandToGraphOutputCommandAsync(commands);
                    ResolveCommands(commands);
                    Debug.Log(JsonConvert.SerializeObject(commands));
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    Debug.LogError("[Pointer] MoveCommand failed.");
                    Debug.LogException(e);
                }
                finally
                {
                    _isResolvingCommand = false;
                }
            }
        }

        #endregion
    }
}