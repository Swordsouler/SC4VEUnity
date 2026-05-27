using Newtonsoft.Json;
using Sc4ve.Voice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent.RuleBased
{
    /// <summary>
    /// Système de reconnaissance d'intention basé uniquement sur des règles algorithmiques,
    /// sans LLM. Utilise les vocabulaires de l'ontologie (annotations, couleurs, déictiques)
    /// pour extraire l'intention et les entités d'une phrase, puis construit directement
    /// les objets Command correspondants.
    ///
    /// Les déclencheurs et la logique de construction des paramètres sont déclarés
    /// directement sur chaque classe Command via [RuleBasedTriggers] et
    /// BuildRuleBasedParameters — ce recognizer est un orchestrateur générique.
    /// </summary>
    public class RuleBasedIntentRecognizer
    {
        // Vocabulaires issus de l'ontologie (forme canonique, ex: "Pomme", "Rouge")
        private readonly List<string> _annotationTypes;
        private readonly List<string> _availableColors;
        private readonly List<string> _pointerDeictics;
        private readonly string _pointerName;
        private readonly string _cameraName;

        // Mots-nombres français → entier
        private static readonly Dictionary<string, int> FrenchNumbers =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "un", 1 }, { "une", 1 }, { "deux", 2 }, { "trois", 3 },
                { "quatre", 4 }, { "cinq", 5 }, { "six", 6 }, { "sept", 7 },
                { "huit", 8 }, { "neuf", 9 }, { "dix", 10 }
            };

        // Pronoms coréférentiels français
        private static readonly HashSet<string> CoreferencePronouns =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "le", "la", "les", "lui", "leur", "eux", "elles",
                "celui-ci", "celle-ci", "ceux-ci", "celles-ci",
                "celui-là", "celle-là", "ceux-là", "celles-là",
                "ça", "cela", "ceci"
            };

        // Mots indiquant une destination (pour MoveCommand)
        private static readonly string[] DestinationWords =
        {
            "là-bas", "là-haut", "ici", "là", "dessus", "dessous",
            "devant", "derrière", "à droite", "à gauche"
        };

        /// <summary>
        /// Délai ajouté au timestamp du PointParameter de destination dans un MoveCommand.
        /// Compense le fait que le pointeur n'est pas encore stabilisé au moment
        /// où l'utilisateur prononce "ici" / "là". La position capturée correspond
        /// alors à la fin de phrase + ce délai, quand le geste est terminé.
        /// </summary>
        private readonly int _movePointDelayMs;

        public RuleBasedIntentRecognizer(
            List<string> annotationTypes,
            List<string> availableColors,
            List<string> pointerDeictics,
            string pointerName,
            string cameraName,
            int movePointDelayMs = 300)
        {
            _annotationTypes = annotationTypes ?? new List<string>();
            _availableColors = availableColors ?? new List<string>();
            _pointerDeictics = pointerDeictics ?? new List<string>();
            _pointerName = pointerName ?? "Pointeur";
            _cameraName = cameraName ?? "Caméra";
            _movePointDelayMs = movePointDelayMs;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Point d'entrée principal
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reconnaît l'intention d'une phrase et retourne un JSON de commandes
        /// au même format que celui produit par le LLM, prêt à être passé à
        /// DeserializeCommand puis CommandToGraphOutputCommandAsync.
        /// Retourne null si aucune intention n'est reconnue.
        /// </summary>
        public string Recognize(Sentence sentence)
        {
            if (sentence == null || string.IsNullOrWhiteSpace(sentence.Text))
                return null;

            string text = sentence.Text.ToLowerInvariant().Trim();
            text = CorrectHomophones(text);
            List<Word> words = sentence.Words ?? new List<Word>();

            // 1. Détection du type de commande via les attributs [RuleBasedTriggers]
            string commandType = DetectCommandType(text);
            if (commandType == null)
            {
                Debug.LogWarning($"[RuleBased] Aucune commande reconnue pour : \"{sentence.Text}\"");
                return null;
            }

            Debug.Log($"[RuleBased] Commande détectée : {commandType} | phrase : \"{sentence.Text}\"");

            // 2. Extraction des entités
            List<RuleBasedAnnotation> annotations = FindAnnotations(text, words);
            List<RuleBasedColor>      colors      = FindColors(text, words);
            List<RuleBasedAnnotation> deictics    = FindDeictics(text, words);
            int  limit          = DetectLimit(text);
            bool hasCoreference = annotations.Count == 0 && deictics.Count == 0 && HasCoreference(text);

            Debug.Log(
                $"[RuleBased] Annotations : [{string.Join(", ", annotations.Select(a => a.Value))}] | " +
                $"Couleurs : [{string.Join(", ", colors.Select(c => $"{c.Value}(cible={c.IsTarget})"))}] | " +
                $"Déictiques : [{string.Join(", ", deictics.Select(d => d.Value))}] | " +
                $"Coréf : {hasCoreference} | Limite : {limit}");

            // 3. Construction de la commande via BuildRuleBasedParameters
            var ctx = new RuleBasedContext
            {
                Text             = text,
                Words            = words,
                PointerName      = _pointerName,
                MovePointDelayMs = _movePointDelayMs,
                Annotations      = annotations,
                Colors           = colors,
                Deictics         = deictics,
                HasCoreference   = hasCoreference,
                Limit            = limit
            };

            Command cmd = CreateCommand(commandType);
            cmd.Parameters = cmd.BuildRuleBasedParameters(ctx);
            if (cmd.Parameters == null)
                return null;

            string json = JsonConvert.SerializeObject(new List<Command> { cmd }, Formatting.Indented);
            Debug.Log($"[RuleBased] JSON produit :\n{json}");
            return json;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Correction des homophones STT
        // ─────────────────────────────────────────────────────────────────────
        //
        // Le STT peut confondre des homophones : "mets" (/mɛ/) → "mais".
        // La grammaire Vosk réduit ces erreurs, mais pas pour les premières
        // phrases arrivant avant que la grammaire soit appliquée.
        // Ce dictionnaire corrige les homophones token par token.

        private static readonly Dictionary<string, string> CommandHomophones =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "mais",  "mets"   },   // /mɛ/  : "mais" (conj.) → "mets" (mettre)
                { "est",   "et"     },   // /ɛ/   : "est" (être)   → "et"   (conj.) — rare
                { "ses",   "ces"    },   // /se/  : "ses" (poss.)  → "ces"  (dém.)
                { "on",    "ont"    },   // /ɔ̃/  : "on" → "ont" — contextuel, désactivé par défaut
            };

        private static string CorrectHomophones(string text)
        {
            string[] tokens = text.Split(' ');
            for (int i = 0; i < tokens.Length; i++)
            {
                if (CommandHomophones.TryGetValue(tokens[i], out string correction))
                    tokens[i] = correction;
            }
            return string.Join(" ", tokens);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Détection du type de commande
        // ─────────────────────────────────────────────────────────────────────

        private string DetectCommandType(string text)
        {
            // Normalisation des accents pour la comparaison
            string normalizedText = FrenchStemmer.NormalizeAccents(text);

            // Stems de chaque token du texte d'entrée (pour la comparaison stemmer)
            string[] inputTokens = normalizedText.Split(
                new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            string[] inputStems = System.Array.ConvertAll(inputTokens, FrenchStemmer.Stem);

            // Pré-vérification : "met(s)/mettre" + mot de destination non-contigu → MoveCommand.
            // Nécessaire quand un pronom ("ça", "le"…) s'intercale entre le verbe et la destination,
            // ce qui empêche les triggers multi-mots ("mets ici", "mets là") de se déclencher.
            string[] clearDestWords = { "ici", "la-bas", "la-haut", "dessus", "dessous", "devant", "derriere", "a droite", "a gauche" };
            if (Regex.IsMatch(normalizedText, @"\b(mets|met|mettre)\b") &&
                clearDestWords.Any(d => normalizedText.Contains(d, StringComparison.OrdinalIgnoreCase)))
            {
                return "MoveCommand";
            }

            // Priorité aux déclencheurs les plus longs (multi-mots d'abord)
            var ordered = RuleBasedTriggersAttribute.GetAllMappings()
                .SelectMany(m => m.Triggers.Select(t => (Trigger: t, CommandType: m.CommandType)))
                .OrderByDescending(x => x.Trigger.Length);

            foreach (var (trigger, commandType) in ordered)
            {
                string normalizedTrigger = FrenchStemmer.NormalizeAccents(trigger);

                // 1. Correspondance exacte (phrase entière ou mot avec frontière)
                if (ContainsPhrase(normalizedText, normalizedTrigger))
                    return commandType;

                // 2. Pour les déclencheurs mono-mot : comparaison des stems
                //    stem("coloris") == stem("colorie") == "color" → match
                if (!trigger.Contains(' '))
                {
                    string triggerStem = FrenchStemmer.Stem(normalizedTrigger);
                    foreach (string inputStem in inputStems)
                    {
                        if (inputStem == triggerStem)
                        {
                            Debug.Log($"[RuleBased/Stem] \"{inputTokens[System.Array.IndexOf(inputStems, inputStem)]}\" " +
                                      $"→ stem \"{inputStem}\" = stem(\"{trigger}\") \"{triggerStem}\" → {commandType}");
                            return commandType;
                        }
                    }
                }
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Extraction des entités
        // ─────────────────────────────────────────────────────────────────────

        private List<RuleBasedAnnotation> FindAnnotations(string text, List<Word> words)
        {
            var result = new List<RuleBasedAnnotation>();
            foreach (string annotation in _annotationTypes)
            {
                string lower = annotation.ToLowerInvariant();
                foreach (string form in GetFrenchForms(lower))
                {
                    if (ContainsPhrase(text, form))
                    {
                        result.Add(new RuleBasedAnnotation
                        {
                            Value     = annotation,
                            Timestamp = GetWordTimestamp(words, form, useStartedAt: false)
                        });
                        break; // une seule occurrence par annotation
                    }
                }
            }
            return result;
        }

        private List<RuleBasedColor> FindColors(string text, List<Word> words)
        {
            var result = new List<RuleBasedColor>();
            foreach (string color in _availableColors)
            {
                string lower = color.ToLowerInvariant();
                foreach (string form in GetFrenchForms(lower))
                {
                    if (ContainsPhrase(text, form))
                    {
                        result.Add(new RuleBasedColor
                        {
                            Value     = color,
                            Timestamp = GetWordTimestamp(words, form, useStartedAt: false),
                            IsTarget  = IsTargetColor(text, form)
                        });
                        break;
                    }
                }
            }
            return result;
        }

        private List<RuleBasedAnnotation> FindDeictics(string text, List<Word> words)
        {
            var result = new List<RuleBasedAnnotation>();
            foreach (string deictic in _pointerDeictics)
            {
                string lower = deictic.ToLowerInvariant().Trim('\'');
                if (ContainsPhrase(text, lower))
                {
                    // Pour un déictique de pointage ("ça", "ceci"…), l'utilisateur pointait
                    // l'objet AVANT de commencer à parler. On utilise le début de la phrase
                    // (words[0].StartedAt) plutôt que le EndedAt du mot déictique, plus robuste
                    // aux imprécisions des timestamps Whisper.
                    DateTime ts = words.Count > 0
                        ? words[0].StartedAt
                        : GetWordTimestamp(words, lower, useStartedAt: true);
                    result.Add(new RuleBasedAnnotation
                    {
                        Value     = _pointerName,
                        Timestamp = ts
                    });
                    break; // un seul déictique suffit
                }
            }
            return result;
        }

        private bool HasCoreference(string text)
        {
            string[] tokens = text.Split(
                new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

            var allMappings = RuleBasedTriggersAttribute.GetAllMappings();
            foreach (string token in tokens)
            {
                // Ignorer les verbes d'action déjà traités
                bool isVerb = allMappings.Any(
                    m => m.Triggers.Any(t => t.Equals(token, StringComparison.OrdinalIgnoreCase)));
                if (isVerb) continue;

                if (CoreferencePronouns.Contains(token))
                    return true;
            }
            return false;
        }

        private int DetectLimit(string text)
        {
            // Nombres en chiffres
            Match m = Regex.Match(text, @"\b(\d+)\b");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int n))
                return n;

            // Nombres en lettres (français)
            string[] tokens = text.Split(
                new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                if (FrenchNumbers.TryGetValue(token, out int num))
                    return num;
            }

            return -1; // tous les objets
        }

        // ─────────────────────────────────────────────────────────────────────
        // Utilitaires
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Vérifie si le texte contient une phrase (multi-mots ou mot unique avec frontière).
        /// </summary>
        private bool ContainsPhrase(string text, string phrase)
        {
            if (phrase.Contains(' '))
                return text.Contains(phrase, StringComparison.OrdinalIgnoreCase);

            return Regex.IsMatch(text, $@"\b{Regex.Escape(phrase)}\b", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Détermine si une couleur trouvée dans le texte est une couleur cible (ex: "en rouge")
        /// plutôt qu'une couleur de filtre source (ex: "les pommes rouges").
        /// </summary>
        private bool IsTargetColor(string text, string colorForm)
        {
            int idx = text.IndexOf(colorForm, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;

            string before = text.Substring(0, idx).TrimEnd();
            return before.EndsWith(" en")
                || before.EndsWith(" de couleur")
                || before == "en";
        }

        /// <summary>
        /// Retourne le timestamp du mot le plus pertinent dans la liste (EndedAt ou StartedAt).
        /// Cherche d'abord une correspondance exacte, puis partielle.
        /// </summary>
        private DateTime GetWordTimestamp(List<Word> words, string wordText, bool useStartedAt)
        {
            if (words == null || words.Count == 0)
                return DateTime.Now;

            // Correspondance exacte
            Word match = words.FirstOrDefault(
                w => w.Text.Equals(wordText, StringComparison.OrdinalIgnoreCase));

            // Correspondance partielle (ex: "pommes" trouvé dans la liste → "pomme")
            if (match == null)
            {
                match = words.FirstOrDefault(w =>
                    w.Text.StartsWith(wordText, StringComparison.OrdinalIgnoreCase) ||
                    wordText.StartsWith(w.Text, StringComparison.OrdinalIgnoreCase));
            }

            if (match == null)
                return useStartedAt ? words[^1].StartedAt : words[^1].EndedAt;

            return useStartedAt ? match.StartedAt : match.EndedAt;
        }

        /// <summary>
        /// Génère les formes flexionnelles françaises courantes d'un mot
        /// (pluriel en -s, -x, -aux, etc.) pour la correspondance textuelle.
        /// </summary>
        private List<string> GetFrenchForms(string baseForm)
        {
            var forms = new List<string> { baseForm };

            if (!baseForm.EndsWith("s") && !baseForm.EndsWith("x"))
            {
                forms.Add(baseForm + "s");  // pomme → pommes

                if (baseForm.EndsWith("eau"))
                    forms.Add(baseForm[..^3] + "eaux"); // gâteau → gâteaux
                else if (baseForm.EndsWith("al"))
                    forms.Add(baseForm[..^2] + "aux");  // cheval → chevaux
            }

            return forms;
        }

        private static Command CreateCommand(string typeName)
        {
            Command cmd = CommandDescriptionAttribute.CreateCommandInstance(typeName);
            cmd.Type = typeName;
            return cmd;
        }
    }
}
