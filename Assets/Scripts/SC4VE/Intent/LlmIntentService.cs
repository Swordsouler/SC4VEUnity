using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Sc4ve.Multimodality.Intent
{
    /// <summary>
    /// Construction du prompt système et appel HTTP « chat/completions » (OpenAI ou serveur
    /// local compatible), SANS dépendance à UnityEngine : la même implémentation sert au
    /// MultimodalityController (exécution en scène) et au harnais d'évaluation de l'extraction
    /// d'intention (tests EditMode), pour que les mesures portent exactement sur le code déployé.
    /// </summary>
    public static class LlmIntentService
    {
        public const string SystemPromptTemplate = @"Tu es un système expert qui convertit le langage naturel en un format de commande JSON pour un environnement 3D.
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

## EXEMPLE 18: Rotation avec angle explicite (propriété ""angle"" de la commande)
Entrée utilisateur:
{{""Text"":""tourne la pomme de 90 degrés"",""Words"":[{{""Text"":""tourne"",""EndedAt"":""2026-03-05T15:00:01.200Z""}},{{""Text"":""la"",""EndedAt"":""2026-03-05T15:00:01.350Z""}},{{""Text"":""pomme"",""EndedAt"":""2026-03-05T15:00:01.800Z""}},{{""Text"":""de"",""EndedAt"":""2026-03-05T15:00:01.950Z""}},{{""Text"":""90"",""EndedAt"":""2026-03-05T15:00:02.300Z""}},{{""Text"":""degrés"",""EndedAt"":""2026-03-05T15:00:02.800Z""}}]}}
JSON Attendu:
[
  {{
    ""type"": ""RotateRightCommand"",
    ""angle"": ""90"",
    ""parameters"": [
      {{
        ""type"": ""SelectionParameter"",
        ""filters"": [
          {{ ""type"": ""Annotation"", ""value"": ""Pomme"", ""timestamp"": ""2026-03-05T15:00:01.800Z"" }}
        ],
        ""limit"": ""-1""
      }}
    ]
  }}
]
Note : le nombre (90) est ici l'AMPLITUDE de l'action (propriété ""angle"" au niveau de la commande), PAS une limite de sélection. Ne jamais produire ""limit"": ""90"" dans ce cas.
--- FIN DES EXEMPLES ---
";

        /// <summary>
        /// Compile le prompt système : normalise les accolades doublées ({{ }}) héritées d'un
        /// ancien string.Format, puis substitue les vocabulaires — mêmes remplacements que
        /// l'ancienne compilation dans MultimodalityController.
        /// </summary>
        public static string BuildSystemPrompt(
            string annotationTypes,
            string availableColors,
            string cameraTerm,
            string pointerTerm,
            string pointerDeictics,
            string availableCommands)
        {
            return SystemPromptTemplate
                // Les exemples JSON du template utilisent des accolades doublées ({{ }}),
                // vestige d'un ancien usage de string.Format. On les normalise en accolades
                // simples (JSON valide) AVANT d'injecter les vocabulaires, pour ne jamais
                // altérer les valeurs substituées (qui ne contiennent pas d'accolades).
                .Replace("{{", "{")
                .Replace("}}", "}")
                .Replace("{annotationTypesString}", annotationTypes)
                .Replace("{availableColorsString}", availableColors)
                .Replace("{cameraTerm}", cameraTerm)
                .Replace("{pointerTerm}", pointerTerm)
                .Replace("{pointerDeicticsString}", pointerDeictics)
                .Replace("{availableCommandsString}", availableCommands);
        }

        /// <summary>
        /// Remplace la section EXEMPLES complète par le seul EXEMPLE 11 (MoveCommand déictique).
        /// EXEMPLE 11 illustre directement la règle 9 : "ça" → filtre Event + StartedAt.
        /// Réduit le prompt de ~6 500 à ~3 200 tokens pour les serveurs locaux à n_ctx limité.
        /// </summary>
        public static string TrimExamplesSection(string prompt)
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
        public static string StripMarkdownJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var match = System.Text.RegularExpressions.Regex.Match(
                text, @"```(?:json)?\s*([\s\S]*?)```",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : text.Trim();
        }

        /// <summary>
        /// Résultat d'un appel chat/completions. <see cref="HttpMs"/> mesure l'aller-retour
        /// HTTP seul (réseau + inférence côté serveur), séparé du reste du traitement — la
        /// thèse signale que la latence des modèles en ligne inclut le réseau : cette mesure
        /// permet au moins d'isoler l'appel du post-traitement local.
        /// </summary>
        public class CallResult
        {
            public string Content;
            public double HttpMs;
            public int PromptTokens;
            public int CompletionTokens;
            public string Error;
        }

        /// <summary>
        /// Appelle l'API chat/completions. <paramref name="endpointBaseUrl"/> null ou vide →
        /// API OpenAI ; sinon URL de base d'un serveur local compatible (ex:
        /// http://localhost:1234/v1). <paramref name="apiKey"/> vide → pas d'en-tête
        /// Authorization (serveur local sans authentification).
        /// </summary>
        public static async Task<CallResult> CallChatCompletionsAsync(
            HttpClient httpClient,
            string endpointBaseUrl,
            string apiKey,
            string model,
            string systemPrompt,
            string userContent,
            bool jsonObjectFormat)
        {
            var requestObject = new JObject
            {
                ["model"]       = model,
                ["messages"]    = new JArray(
                    new JObject { ["role"] = "system", ["content"] = systemPrompt },
                    new JObject { ["role"] = "user",   ["content"] = userContent }
                ),
                ["temperature"] = 0.0,
                ["max_tokens"]  = 2048
            };
            // json_object est supporté par OpenAI gpt-4o/mini mais pas par tous les serveurs
            // locaux. StripMarkdownJson gère le cas où le modèle emballe quand même en markdown.
            if (jsonObjectFormat)
                requestObject["response_format"] = new JObject { ["type"] = "json_object" };

            string url = string.IsNullOrWhiteSpace(endpointBaseUrl)
                ? "https://api.openai.com/v1/chat/completions"
                : endpointBaseUrl.TrimEnd('/') + "/chat/completions";

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            if (!string.IsNullOrWhiteSpace(apiKey))
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            requestMessage.Content = new StringContent(
                JsonConvert.SerializeObject(requestObject), Encoding.UTF8, "application/json");

            var result = new CallResult();
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
                string body = await response.Content.ReadAsStringAsync();
                stopwatch.Stop();
                result.HttpMs = stopwatch.Elapsed.TotalMilliseconds;

                if (!response.IsSuccessStatusCode)
                {
                    result.Error = $"{(int)response.StatusCode} {response.StatusCode} @ {url}\n{body}";
                    return result;
                }

                JObject parsed = JObject.Parse(body);
                result.PromptTokens     = (int?)parsed["usage"]?["prompt_tokens"] ?? 0;
                result.CompletionTokens = (int?)parsed["usage"]?["completion_tokens"] ?? 0;

                // Un « choices »: [] arrive (filtre de contenu, certains serveurs locaux) :
                // sans cette garde, l'indexation lèverait au lieu d'un repli gracieux.
                string content = (string)(parsed["choices"] as JArray)?.FirstOrDefault()?["message"]?["content"];
                if (content == null)
                {
                    result.Error = $"Réponse sans 'choices' exploitable ({model} @ {url}) : {body}";
                    return result;
                }
                result.Content = StripMarkdownJson(content);
                return result;
            }
            catch (Exception e)
            {
                // HttpRequestException (réseau) et TaskCanceledException (timeout HttpClient)
                // sont remontées comme erreur textuelle : l'appelant décide du repli.
                stopwatch.Stop();
                result.HttpMs = stopwatch.Elapsed.TotalMilliseconds;
                result.Error = $"{e.GetType().Name}: {e.Message} @ {url}";
                return result;
            }
        }
    }
}
