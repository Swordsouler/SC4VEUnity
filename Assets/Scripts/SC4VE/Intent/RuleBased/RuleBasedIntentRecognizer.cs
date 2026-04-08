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

        // Verbes d'action → type de commande
        // Ordre : les déclencheurs multi-mots passent en premier (plus long → plus prioritaire)
        private static readonly List<(string[] Triggers, string CommandType)> ActionMappings =
            new()
            {
                // ColorizeCopyCommand (avant Colorize pour éviter le sous-match)
                (new[] { "copie la couleur", "colorie comme", "même couleur que", "copier la couleur" },
                    "ColorizeCopyCommand"),
                // ColorizeDarkerCommand
                (new[] { "rends plus sombre", "rend plus sombre", "assombris", "assombrit",
                          "noircis", "noircit", "fonce", "foncer", "obscurcis", "obscurcit" },
                    "ColorizeDarkerCommand"),
                // ColorizeLighterCommand
                (new[] { "rends plus clair", "rend plus clair", "éclaircis", "éclaircit",
                          "clarifies", "illumine", "éclaire" },
                    "ColorizeLighterCommand"),
                // ColorizeCommand
                (new[] { "change la couleur", "met en couleur", "mets en couleur",
                          "colorie", "coloris", "colorise", "colorisez", "coloriez",
                          "peins", "peinez", "recolore", "recolorez",
                          "colorier", "coloriser" },
                    "ColorizeCommand"),
                // HideCommand
                (new[] { "rend invisible", "rends invisible", "masque", "cache", "dissimule",
                          "masquer", "cacher", "dissumuler", "invisibilise", "invisibiliser" },
                    "HideCommand"),
                // ShowCommand
                (new[] { "rend visible", "rends visible", "montre", "affiche", "révèle",
                          "démasque", "montrer", "afficher", "révéler", "démasquer" },
                    "ShowCommand"),
                // ScaleUpCommand
                (new[] { "augmente la taille", "scale up", "grossis", "grossit", "agrandis",
                          "agrandit", "grandit", "grandir", "grossir", "agrandir" },
                    "ScaleUpCommand"),
                // ScaleDownCommand
                (new[] { "diminue la taille", "scale down", "rapetisse", "rapetissit", "réduis",
                          "réduit", "diminue", "rétrécis", "rétrécit", "rapetisser", "réduire",
                          "rétrécir" },
                    "ScaleDownCommand"),
                // MoveCommand
                (new[] { "déplace", "déplacer", "bouge", "bouger", "amène", "amener",
                          "move", "repositionne", "repositionner", "transporte", "transporter" },
                    "MoveCommand"),
                // DuplicateCommand (avant Copy pour éviter "copie" → DuplicateCommand)
                (new[] { "duplique", "dupliquer", "clone", "cloner", "crée une copie",
                          "créer une copie" },
                    "DuplicateCommand"),
                // GrabCommand
                (new[] { "attrape", "attraper", "prends", "prendre", "grab",
                          "saisit", "saisir", "empare", "emparer" },
                    "GrabCommand"),
                // ReleaseCommand
                (new[] { "lâche", "lâcher", "pose", "poser", "release",
                          "libère", "libérer", "dépose", "déposer" },
                    "ReleaseCommand"),
                // UnselectCommand (avant Select pour éviter le sous-match)
                (new[] { "désélectionne", "désélectionner", "unselect",
                          "démarque", "démarquer" },
                    "UnselectCommand"),
                // SelectCommand
                (new[] { "sélectionne", "sélectionner", "select",
                          "choisis", "choisir", "marque", "marquer" },
                    "SelectCommand"),
                // MeasureCommand
                (new[] { "mesure", "mesurer", "calcule la distance", "calculer la distance",
                          "quelle est la distance" },
                    "MeasureCommand"),
            };

        // Mots indiquant une destination (pour MoveCommand)
        private static readonly string[] DestinationWords =
        {
            "là-bas", "là-haut", "ici", "là", "dessus", "dessous",
            "devant", "derrière", "à droite", "à gauche"
        };

        public RuleBasedIntentRecognizer(
            List<string> annotationTypes,
            List<string> availableColors,
            List<string> pointerDeictics,
            string pointerName,
            string cameraName)
        {
            _annotationTypes = annotationTypes ?? new List<string>();
            _availableColors = availableColors ?? new List<string>();
            _pointerDeictics = pointerDeictics ?? new List<string>();
            _pointerName = pointerName ?? "Pointeur";
            _cameraName = cameraName ?? "Caméra";
        }

        // ─────────────────────────────────────────────────────────────────────
        // Point d'entrée principal
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reconnaît l'intention d'une phrase et retourne la liste de commandes correspondante.
        /// Retourne null si aucune intention n'est reconnue.
        /// </summary>
        public List<Command> Recognize(Sentence sentence)
        {
            if (sentence == null || string.IsNullOrWhiteSpace(sentence.Text))
                return null;

            string text = sentence.Text.ToLowerInvariant().Trim();
            List<Word> words = sentence.Words ?? new List<Word>();

            // 1. Détection du type de commande via les verbes d'action
            string commandType = DetectCommandType(text);
            if (commandType == null)
            {
                Debug.LogWarning($"[RuleBased] Aucune commande reconnue pour : \"{sentence.Text}\"");
                return null;
            }

            Debug.Log($"[RuleBased] Commande détectée : {commandType} | phrase : \"{sentence.Text}\"");

            // 2. Extraction des entités
            List<AnnotationMatch> annotations = FindAnnotations(text, words);
            List<ColorMatch> colors = FindColors(text, words);
            List<AnnotationMatch> deictics = FindDeictics(text, words);
            int limit = DetectLimit(text);

            // Coréférence : uniquement si aucune annotation ET aucun déictique
            bool hasCoreference = annotations.Count == 0
                               && deictics.Count == 0
                               && HasCoreference(text);

            Debug.Log(
                $"[RuleBased] Annotations : [{string.Join(", ", annotations.Select(a => a.Value))}] | " +
                $"Couleurs : [{string.Join(", ", colors.Select(c => $"{c.Value}(cible={c.IsTarget})"))}] | " +
                $"Déictiques : [{string.Join(", ", deictics.Select(d => d.Value))}] | " +
                $"Coréf : {hasCoreference} | Limite : {limit}");

            // 3. Construction des commandes
            return BuildCommands(commandType, annotations, colors, deictics,
                                 hasCoreference, limit, words, text);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Détection du type de commande
        // ─────────────────────────────────────────────────────────────────────

        private string DetectCommandType(string text)
        {
            // Priorité aux déclencheurs les plus longs (multi-mots d'abord)
            var ordered = ActionMappings
                .SelectMany(m => m.Triggers.Select(t => (Trigger: t, m.CommandType)))
                .OrderByDescending(x => x.Trigger.Length);

            foreach (var (trigger, commandType) in ordered)
            {
                if (ContainsPhrase(text, trigger))
                    return commandType;
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Extraction des entités
        // ─────────────────────────────────────────────────────────────────────

        private List<AnnotationMatch> FindAnnotations(string text, List<Word> words)
        {
            var result = new List<AnnotationMatch>();
            foreach (string annotation in _annotationTypes)
            {
                string lower = annotation.ToLowerInvariant();
                foreach (string form in GetFrenchForms(lower))
                {
                    if (ContainsPhrase(text, form))
                    {
                        result.Add(new AnnotationMatch
                        {
                            Value = annotation,
                            Timestamp = GetWordTimestamp(words, form, useStartedAt: false)
                        });
                        break; // une seule occurrence par annotation
                    }
                }
            }
            return result;
        }

        private List<ColorMatch> FindColors(string text, List<Word> words)
        {
            var result = new List<ColorMatch>();
            foreach (string color in _availableColors)
            {
                string lower = color.ToLowerInvariant();
                foreach (string form in GetFrenchForms(lower))
                {
                    if (ContainsPhrase(text, form))
                    {
                        result.Add(new ColorMatch
                        {
                            Value = color,
                            Timestamp = GetWordTimestamp(words, form, useStartedAt: false),
                            IsTarget = IsTargetColor(text, form)
                        });
                        break;
                    }
                }
            }
            return result;
        }

        private List<AnnotationMatch> FindDeictics(string text, List<Word> words)
        {
            var result = new List<AnnotationMatch>();
            foreach (string deictic in _pointerDeictics)
            {
                string lower = deictic.ToLowerInvariant().Trim('\'');
                if (ContainsPhrase(text, lower))
                {
                    result.Add(new AnnotationMatch
                    {
                        Value = _pointerName,
                        Timestamp = GetWordTimestamp(words, lower, useStartedAt: false)
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

            foreach (string token in tokens)
            {
                // Ignorer les verbes d'action déjà traités
                bool isVerb = ActionMappings.Any(
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
        // Construction des commandes
        // ─────────────────────────────────────────────────────────────────────

        private List<Command> BuildCommands(
            string commandType,
            List<AnnotationMatch> annotations,
            List<ColorMatch> colors,
            List<AnnotationMatch> deictics,
            bool hasCoreference,
            int limit,
            List<Word> words,
            string text)
        {
            var commands = new List<Command>();

            switch (commandType)
            {
                case "ColorizeCommand":
                    {
                        List<ColorMatch> targetColors = colors.Where(c => c.IsTarget).ToList();
                        List<ColorMatch> sourceColors = colors.Where(c => !c.IsTarget).ToList();

                        // La couleur cible va dans ColorParameter ; la couleur source filtre la sélection
                        SelectionParameter selParam = BuildSelectionParameter(
                            annotations, sourceColors, deictics, hasCoreference, limit, useStartedAt: false);

                        Command cmd = CreateCommand("ColorizeCommand");
                        cmd.Parameters = new List<Parameter>();

                        if (targetColors.Count > 0)
                        {
                            cmd.Parameters.Add(new ColorParameter
                            {
                                Type = "ColorParameter",
                                Value = targetColors[0].Value,
                                Timestamp = targetColors[0].Timestamp
                            });
                        }
                        cmd.Parameters.Add(selParam);
                        commands.Add(cmd);
                        break;
                    }

                case "ColorizeCopyCommand":
                case "ColorizeDarkerCommand":
                case "ColorizeLighterCommand":
                    {
                        SelectionParameter selParam = BuildSelectionParameter(
                            annotations, colors.Where(c => !c.IsTarget).ToList(),
                            deictics, hasCoreference, limit, useStartedAt: false);

                        Command cmd = CreateCommand(commandType);
                        cmd.Parameters = new List<Parameter> { selParam };
                        commands.Add(cmd);
                        break;
                    }

                case "MoveCommand":
                    {
                        // Pour MoveCommand, le sélecteur source utilise StartedAt du déictique
                        SelectionParameter selParam = BuildSelectionParameter(
                            annotations, colors.Where(c => !c.IsTarget).ToList(),
                            deictics, hasCoreference, limit, useStartedAt: true);

                        // Recherche du mot de destination (ici, là, etc.)
                        string destWord = FindDestinationWord(text);
                        DateTime destTs = GetWordTimestamp(words, destWord, useStartedAt: false);

                        var pointParam = new PointParameter
                        {
                            Type = "PointParameter",
                            Value = _pointerName,
                            Timestamp = destTs
                        };

                        Command cmd = CreateCommand("MoveCommand");
                        cmd.Parameters = new List<Parameter> { selParam, pointParam };
                        commands.Add(cmd);
                        break;
                    }

                case "GrabCommand":
                    {
                        SelectionParameter selParam = BuildSelectionParameter(
                            annotations, colors.Where(c => !c.IsTarget).ToList(),
                            deictics, hasCoreference, limit, useStartedAt: false);

                        DateTime grabTs = deictics.Count > 0
                            ? deictics[0].Timestamp
                            : (words.Count > 0 ? words[^1].EndedAt : DateTime.Now);

                        var pointParam = new PointParameter
                        {
                            Type = "PointParameter",
                            Value = _pointerName,
                            Timestamp = grabTs
                        };

                        Command cmd = CreateCommand("GrabCommand");
                        cmd.Parameters = new List<Parameter> { selParam, pointParam };
                        commands.Add(cmd);
                        break;
                    }

                default:
                    {
                        // HideCommand, ShowCommand, ScaleUpCommand, ScaleDownCommand,
                        // DuplicateCommand, ReleaseCommand, SelectCommand, UnselectCommand,
                        // MeasureCommand
                        SelectionParameter selParam = BuildSelectionParameter(
                            annotations, colors.Where(c => !c.IsTarget).ToList(),
                            deictics, hasCoreference, limit, useStartedAt: false);

                        Command cmd = CreateCommand(commandType);
                        cmd.Parameters = new List<Parameter> { selParam };
                        commands.Add(cmd);
                        break;
                    }
            }

            return commands;
        }

        private SelectionParameter BuildSelectionParameter(
            List<AnnotationMatch> annotations,
            List<ColorMatch> sourceColors,
            List<AnnotationMatch> deictics,
            bool hasCoreference,
            int limit,
            bool useStartedAt)
        {
            var filters = new List<FilterElement>();

            if (hasCoreference)
            {
                // Coréférence exclusive : aucun autre filtre
                filters.Add(new FilterElement
                {
                    IsOperator = false,
                    Condition = new Condition { Type = "Coreference" }
                });
            }
            else
            {
                bool needsOperator = false;

                // Filtres Event (déictiques)
                foreach (AnnotationMatch deictic in deictics)
                {
                    if (needsOperator)
                        filters.Add(new FilterElement { IsOperator = true, Operator = "AND" });

                    filters.Add(new FilterElement
                    {
                        IsOperator = false,
                        Condition = new Condition
                        {
                            Type = "Event",
                            Value = deictic.Value,
                            Timestamp = deictic.Timestamp
                        }
                    });
                    needsOperator = true;
                }

                // Filtres Annotation
                foreach (AnnotationMatch annotation in annotations)
                {
                    if (needsOperator)
                        filters.Add(new FilterElement { IsOperator = true, Operator = "AND" });

                    filters.Add(new FilterElement
                    {
                        IsOperator = false,
                        Condition = new Condition
                        {
                            Type = "Annotation",
                            Value = annotation.Value,
                            Timestamp = annotation.Timestamp
                        }
                    });
                    needsOperator = true;
                }

                // Filtres Color (couleur source)
                foreach (ColorMatch color in sourceColors)
                {
                    if (needsOperator)
                        filters.Add(new FilterElement { IsOperator = true, Operator = "AND" });

                    filters.Add(new FilterElement
                    {
                        IsOperator = false,
                        Condition = new Condition
                        {
                            Type = "Color",
                            Value = color.Value,
                            Timestamp = color.Timestamp
                        }
                    });
                    needsOperator = true;
                }
            }

            return new SelectionParameter
            {
                Type = "SelectionParameter",
                Filters = filters,
                Limit = limit
            };
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

        /// <summary>
        /// Cherche un mot de destination dans le texte (pour MoveCommand).
        /// </summary>
        private string FindDestinationWord(string text)
        {
            // Ordre : du plus long au plus court pour priorité
            foreach (string dest in DestinationWords.OrderByDescending(d => d.Length))
            {
                if (text.Contains(dest, StringComparison.OrdinalIgnoreCase))
                    return dest;
            }
            return "ici"; // valeur par défaut
        }

        private static Command CreateCommand(string typeName)
        {
            Command cmd = CommandDescriptionAttribute.CreateCommandInstance(typeName);
            cmd.Type = typeName;
            return cmd;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Structures de données internes
        // ─────────────────────────────────────────────────────────────────────

        private struct AnnotationMatch
        {
            public string Value;
            public DateTime Timestamp;
        }

        private struct ColorMatch
        {
            public string Value;
            public DateTime Timestamp;
            public bool IsTarget; // true → ColorParameter ; false → filtre source
        }
    }
}
