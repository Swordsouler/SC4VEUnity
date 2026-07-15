using Newtonsoft.Json;
using Sven.Context;
using Sven.Utils;
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

        // Mots-nombres → entier, par langue (la table active suit la locale).
        private static readonly Dictionary<string, int> NumbersFr =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "un", 1 }, { "une", 1 }, { "deux", 2 }, { "trois", 3 },
                { "quatre", 4 }, { "cinq", 5 }, { "six", 6 }, { "sept", 7 },
                { "huit", 8 }, { "neuf", 9 }, { "dix", 10 }
            };
        private static readonly Dictionary<string, int> NumbersEn =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "one", 1 }, { "two", 2 }, { "three", 3 }, { "four", 4 }, { "five", 5 },
                { "six", 6 }, { "seven", 7 }, { "eight", 8 }, { "nine", 9 }, { "ten", 10 }
            };
        private static Dictionary<string, int> Numbers => IsFrench ? NumbersFr : NumbersEn;

        // Pronoms coréférentiels, par langue.
        private static readonly HashSet<string> CoreferencePronounsFr =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "le", "la", "les", "lui", "leur", "eux", "elles",
                "celui-ci", "celle-ci", "ceux-ci", "celles-ci",
                "celui-là", "celle-là", "ceux-là", "celles-là",
                "ça", "cela", "ceci"
            };
        private static readonly HashSet<string> CoreferencePronounsEn =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "it", "them", "they", "this", "that", "these", "those", "one", "ones"
            };
        private static HashSet<string> CoreferencePronouns => IsFrench ? CoreferencePronounsFr : CoreferencePronounsEn;

        // Mots indiquant une destination (pour MoveCommand), par langue.
        private static readonly string[] DestinationWordsFr =
        {
            "là-bas", "là-haut", "ici", "là", "dessus", "dessous",
            "devant", "derrière", "à droite", "à gauche"
        };
        private static readonly string[] DestinationWordsEn =
        {
            "over there", "up there", "here", "there", "on top", "underneath",
            "in front", "behind", "to the right", "to the left"
        };
        private static string[] DestinationWords => IsFrench ? DestinationWordsFr : DestinationWordsEn;

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

            // Référence explicite à la sélection courante : « les objets (actuellement)
            // sélectionnés », « la sélection ». Le participe « sélectionné(e)(s) » (accent
            // final) et le nom « sélection » sont distingués du verbe « sélectionne(r) » par
            // l'accent et la frontière de mot. On retire ces mots du texte de détection de
            // commande (sinon « sélectionné » déclenche SelectCommand, car NormalizeAccents
            // confond « sélectionné » et « sélectionne »), et on force la coréférence.
            // « sélectionné(e)(s) » / « sélection » (fr) ; « selected » / « selection » (en).
            string selectionRefPattern = IsFrench
                ? @"\b(sélectionné(e|s|es)?|sélection)\b"
                : @"\b(selected|selection)\b";
            bool referencesSelection = Regex.IsMatch(text, selectionRefPattern);
            string commandText = referencesSelection
                ? Regex.Replace(text, selectionRefPattern, " ")
                : text;

            // 1. Détection du type de commande via les attributs [RuleBasedTriggers]
            string commandType = DetectCommandType(commandText);
            if (commandType == null)
            {
                Debug.LogWarning($"[RuleBased] Aucune commande reconnue pour : \"{sentence.Text}\"");
                return null;
            }

            Debug.Log($"[RuleBased] Commande détectée : {commandType} | phrase : \"{sentence.Text}\"");

            // 2. Extraction des entités
            List<RuleBasedAnnotation> annotations = FindAnnotations(text, words);
            List<RuleBasedColor>      colors      = FindColors(text, words);
            // Ablation (benchmark) : pointage désactivé → pas de déictiques (« ça » ne produit
            // plus de filtre Event), la résolution se fait à la voix seule.
            List<RuleBasedAnnotation> deictics    = MultimodalitySettings.PointingEnabled
                ? FindDeictics(text, words)
                : new List<RuleBasedAnnotation>();
            int  limit          = DetectLimit(text);
            // Intention au singulier (« la pomme ») : aucun marqueur pluriel NI nombre. Sert à la
            // désambiguïsation quand plusieurs cibles correspondent (cf. ResolveCommands).
            bool singularIntent = !HasPluralMarker(text) && limit <= 1;
            // Une référence explicite à la sélection (« …sélectionnés », « la sélection »)
            // force la coréférence vers la sélection courante.
            bool hasCoreference = referencesSelection
                || (annotations.Count == 0 && deictics.Count == 0 && HasCoreference(text));

            // « sélectionne toutes les citrouilles » : « tout/toutes » quantifie ici un type
            // précis (annotation, couleur ou déictique) → ce n'est pas « tout sélectionner »
            // mais une sélection filtrée sur ce type. On requalifie SelectAll en Select (limite
            // -1 = tous les objets de ce type).
            if (commandType == "SelectAllCommand" &&
                (annotations.Count > 0 || colors.Count > 0 || deictics.Count > 0))
            {
                Debug.Log("[RuleBased] SelectAllCommand requalifié en SelectCommand (cible spécifique présente).");
                commandType = "SelectCommand";
            }

            // « mets la taille à 50 » (ScaleToCommand) : le nombre est la VALEUR d'échelle absolue,
            // pas une limite de sélection. On l'extrait et on remet la limite à « tous ».
            float scaleValue = 0f;
            if (commandType == "ScaleToCommand" && limit > 0)
            {
                scaleValue = limit;
                limit = -1;
            }

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
                Limit            = limit,
                ScaleFactor      = DetectScaleFactor(text),
                ScaleValue       = scaleValue,
                SingularIntent   = singularIntent
            };

            Command cmd = CreateCommand(commandType);
            cmd.Parameters = cmd.BuildRuleBasedParameters(ctx);
            if (cmd.Parameters == null)
                return null;

            string json = JsonConvert.SerializeObject(new List<Command> { cmd }, Formatting.Indented);
            Debug.Log($"[RuleBased] JSON produit :\n{json}");
            return json;
        }

        /// <summary>
        /// Tente de compléter une commande en attente de clarification avec le paramètre fourni
        /// par cette phrase-réponse (« en bleu » → couleur ; « là-bas » → destination).
        /// Retourne le JSON de la commande complétée, ou null si la phrase n'apporte pas le
        /// paramètre manquant. Permet le dialogue : « Colorie cette banane » → « En bleu ».
        /// </summary>
        public string CompletePending(Sentence sentence, Command pending)
        {
            if (sentence == null || string.IsNullOrWhiteSpace(sentence.Text) || pending == null)
                return null;

            string text = CorrectHomophones(sentence.Text.ToLowerInvariant().Trim());
            List<Word> words = sentence.Words ?? new List<Word>();
            var ps = new List<Parameter>(pending.Parameters ?? new List<Parameter>());
            bool filled = false;

            // Réponse de type couleur (ex: ColorizeCommand en attente → « en bleu »).
            if (ps.OfType<ColorParameter>().FirstOrDefault() == null)
            {
                RuleBasedColor color = FindColors(text, words).FirstOrDefault();
                if (color.Value != null)
                {
                    ps.Insert(0, new ColorParameter { Type = "ColorParameter", Value = color.Value, Timestamp = color.Timestamp });
                    filled = true;
                }
            }

            // Réponse de type destination (ex: MoveCommand en attente → « là-bas » / pointage).
            if (!filled && ps.OfType<PointParameter>().FirstOrDefault() == null &&
                DestinationWords.Any(w => Regex.IsMatch(text, $@"\b{Regex.Escape(w)}\b", RegexOptions.IgnoreCase)))
            {
                DateTime end = words.Count > 0 ? words[^1].EndedAt : DateTime.Now;
                ps.Add(new PointParameter { Type = "PointParameter", Value = _pointerName,
                                            Timestamp = end.AddMilliseconds(_movePointDelayMs) });
                filled = true;
            }

            if (!filled) return null;

            pending.Parameters = ps;
            string completed = JsonConvert.SerializeObject(new List<Command> { pending }, Formatting.Indented);
            Debug.Log($"[RuleBased] Complétion de {pending.Type} :\n{completed}");
            return completed;
        }

        /// <summary>
        /// Si la phrase contient un paramètre isolé (couleur) mais aucune commande reconnue,
        /// retourne le nom de la classe de paramètre (« ColorParameter ») pour une clarification
        /// d'ambiguïté (« en vert » → colorier ? sélectionner ?) ; sinon null. À appeler quand
        /// Recognize a renvoyé null et qu'aucune commande n'est en attente.
        /// </summary>
        public string DetectOrphanParameter(Sentence sentence)
        {
            if (sentence == null || string.IsNullOrWhiteSpace(sentence.Text))
                return null;
            string text = CorrectHomophones(sentence.Text.ToLowerInvariant().Trim());
            List<Word> words = sentence.Words ?? new List<Word>();
            if (FindColors(text, words).Count > 0)
                return "ColorParameter";
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Correction des confusions STT — PAR LANGUE
        // ─────────────────────────────────────────────────────────────────────
        //
        // Le STT confond certains mots (Whisper : « déplace » → « dépasse » ; Vosk : « mets »
        // → « mais »…). On corrige token par token AVANT la détection. Ces corrections sont
        // PROPRES À CHAQUE LANGUE : les appliquer à une autre langue corromprait l'entrée
        // (ex: l'anglais « on » deviendrait le français « ont »). La table suit la locale active.

        private static readonly Dictionary<string, string> HomophonesFr =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Uniquement des corrections liées aux verbes de commande (sûres : ces mots ne
                // sont pas utilisés autrement dans une commande). On évite les particules
                // courantes (est/et, ses/ces, on/ont) qui pourraient corrompre une entrée valide.
                { "mais",     "mets"     },   // /mɛ/ : "mais" → "mets" (mettre)
                { "dépasse",  "déplace"  },   // Whisper confond /deplas/ (déplace) et /depas/ (dépasse)
                { "dépasser", "déplacer" },
                { "dépassé",  "déplacé"  },
            };

        // Confusions propres à l'anglais — à compléter au fil des tests (vide = aucune correction).
        private static readonly Dictionary<string, string> HomophonesEn =
            new(StringComparer.OrdinalIgnoreCase) { };

        // Table de correction de la langue active.
        private static Dictionary<string, string> ActiveHomophones => IsFrench ? HomophonesFr : HomophonesEn;

        private static string CorrectHomophones(string text)
        {
            string[] tokens = text.Split(' ');
            for (int i = 0; i < tokens.Length; i++)
            {
                if (ActiveHomophones.TryGetValue(tokens[i], out string correction))
                    tokens[i] = correction;
            }
            return string.Join(" ", tokens);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Détection du type de commande
        // ─────────────────────────────────────────────────────────────────────

        // Stemming dépendant de la locale : le stemmer français n'est appliqué qu'en français.
        // Pour les autres langues on garde le token normalisé (correspondance exacte), car
        // FrenchStemmer produirait des racines erronées hors français. Défaut = français.
        // Vrai si la locale active est le français (défaut). Sert au stemming ET au choix de
        // la table de corrections STT.
        private static bool IsFrench =>
            string.IsNullOrEmpty(UserData.Locale) || UserData.Locale.StartsWith("fr");
        private static string Stem(string normalized) =>
            IsFrench ? FrenchStemmer.Stem(normalized) : normalized;

        private string DetectCommandType(string text)
        {
            // Normalisation des accents pour la comparaison
            string normalizedText = FrenchStemmer.NormalizeAccents(text);

            // Stems de chaque token du texte d'entrée (pour la comparaison stemmer)
            string[] inputTokens = normalizedText.Split(
                new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            string[] inputStems = System.Array.ConvertAll(inputTokens, Stem);

            // Pré-vérification : « (mets/règle/fixe) la taille … à N » → régler la taille à une
            // valeur absolue. AVANT MoveCommand : sans accent, le trigger « mets là » devient
            // « mets la » et matcherait « mets la taille ». « taille »/« size » + un nombre lève
            // l'ambiguïté (« double/triple la taille » n'a pas de nombre → reste ScaleUp).
            string sizeWord = IsFrench ? "taille" : "size";
            if (Regex.IsMatch(normalizedText, $@"\b{sizeWord}\b") && DetectLimit(text) > 0)
                return "ScaleToCommand";

            // Pré-vérification : "met(s)/mettre" + mot de destination non-contigu → MoveCommand.
            // Nécessaire quand un pronom ("ça", "le"…) s'intercale entre le verbe et la destination,
            // ce qui empêche les triggers multi-mots ("mets ici", "mets là") de se déclencher.
            string moveVerbs = IsFrench ? @"\b(mets|met|mettre)\b" : @"\b(put|move|place)\b";
            string[] clearDestWords = IsFrench
                ? new[] { "ici", "la-bas", "la-haut", "dessus", "dessous", "devant", "derriere", "a droite", "a gauche" }
                : new[] { "here", "there", "over there", "up there", "on top", "underneath", "in front", "behind", "to the right", "to the left" };
            if (Regex.IsMatch(normalizedText, moveVerbs) &&
                clearDestWords.Any(d => Regex.IsMatch(normalizedText, $@"\b{Regex.Escape(d)}\b", RegexOptions.IgnoreCase)))
            {
                return "MoveCommand";
            }

            // Priorité aux déclencheurs les plus longs (multi-mots d'abord)
            var ordered = CommandVocabulary.TriggerMappings
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
                    string triggerStem = Stem(normalizedTrigger);
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

            var allMappings = CommandVocabulary.TriggerMappings;
            bool IsVerb(string w) => allMappings.Any(
                m => m.Triggers.Any(t => t.Equals(w, StringComparison.OrdinalIgnoreCase)));

            foreach (string token in tokens)
            {
                // Token entier : gère les pronoms composés (« celui-ci »…) et simples (« les », « ça »).
                if (!IsVerb(token) && CoreferencePronouns.Contains(token))
                    return true;

                // Pronom enclitique accolé à l'impératif : « mets-les », « colorie-le », « cache-la ».
                // Le STT (Whisper) rend « mets-les » en un seul token ; on découpe sur le trait
                // d'union et on teste chaque partie (le verbe est ignoré).
                if (token.Contains('-'))
                    foreach (string part in token.Split('-'))
                        if (!IsVerb(part) && CoreferencePronouns.Contains(part))
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
                if (Numbers.TryGetValue(token, out int num))
                    return num;
            }

            return -1; // tous les objets
        }

        /// <summary>
        /// Facteur d'échelle explicite : « double(r) » → 2, « triple(r) » → 3 ; 0 si non spécifié
        /// (la commande applique alors son facteur incrémental par défaut). Les radicaux « doubl »
        /// / « tripl » couvrent les formes fr ET en (mots quasi identiques).
        /// </summary>
        private static float DetectScaleFactor(string text)
        {
            string n = FrenchStemmer.NormalizeAccents(text);
            if (Regex.IsMatch(n, @"\bdoubl")) return 2f;
            if (Regex.IsMatch(n, @"\btripl")) return 3f;
            return 0f;
        }

        /// <summary>
        /// Vrai si la phrase contient un marqueur de pluralité / collectif (« les », « tous »… ;
        /// « all », « every »… ou « the …s ») → intention « tous les objets de ce type », pas une
        /// cible unique. L'anglais reste approximatif (pluriel du nom mal détecté hors collectifs).
        /// </summary>
        private static bool HasPluralMarker(string text)
        {
            string n = FrenchStemmer.NormalizeAccents(text);
            string pattern = IsFrench
                ? @"\b(les|des|ces|tous|toutes|tout|plusieurs)\b"
                : @"\b(all|every|both|several)\b|\bthe\s+\w+s\b";
            return Regex.IsMatch(n, pattern);
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
        /// Détermine si une couleur est la couleur CIBLE (à appliquer) plutôt qu'un filtre SOURCE
        /// (décrivant les objets). FR : introduite par « en » / « de couleur » (« …en rouge »).
        /// EN : pas de préposition fiable (« color it red ») → la cible est en fin de phrase ;
        /// une couleur source précède l'objet (« color the green apples red »).
        /// </summary>
        private bool IsTargetColor(string text, string colorForm)
        {
            int idx = text.IndexOf(colorForm, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;

            string before = text.Substring(0, idx).TrimEnd();
            if (IsFrench)
                return before.EndsWith(" en") || before.EndsWith(" de couleur") || before == "en";

            // Anglais : couleur en fin de phrase, ou introduite par « in »/« to ».
            string after = text.Substring(idx + colorForm.Length).Trim();
            return after.Length == 0 || before.EndsWith(" in") || before.EndsWith(" to");
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
