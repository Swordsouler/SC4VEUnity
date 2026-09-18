using Newtonsoft.Json;
using Sven.Context;
using Sven.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        // Prénoms des objets nommables de la scène (les clients) — voir FindNames.
        private readonly List<string> _objectNames;
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

        /// <summary>
        /// Les recettes connues, triées par longueur de libellé DÉCROISSANTE : « soupe de
        /// carottes » doit être essayée avant « soupe », faute de quoi la famille l'emporterait
        /// sur la recette précise (§6.4 du README).
        /// </summary>
        private readonly List<RecipeVocabulary.Recipe> _recipes;

        public RuleBasedIntentRecognizer(
            List<string> annotationTypes,
            List<string> availableColors,
            List<string> pointerDeictics,
            string pointerName,
            string cameraName,
            int movePointDelayMs = 300,
            List<RecipeVocabulary.Recipe> recipes = null,
            List<string> objectNames = null)
        {
            // Du libellé le plus long au plus court, comme les recettes et les déclencheurs :
            // « Pomme de terre » doit être essayé avant « Pomme », sans quoi « mets la pomme de
            // terre dans l'assiette » produit DEUX annotations — une patate et une pomme — et
            // la sélection ramasse tous les fruits de la table.
            _annotationTypes = (annotationTypes ?? new List<string>())
                .OrderByDescending(a => a.Length)
                .ToList();
            _availableColors = availableColors ?? new List<string>();
            _pointerDeictics = pointerDeictics ?? new List<string>();
            _pointerName = pointerName ?? "Pointeur";
            _cameraName = cameraName ?? "Caméra";
            _movePointDelayMs = movePointDelayMs;
            _recipes = (recipes ?? new List<RecipeVocabulary.Recipe>())
                .OrderByDescending(r => r.Label.Length)
                .ToList();
            // Comme le reste du vocabulaire : du plus long au plus court, pour qu'un prénom
            // qui en contient un autre (« Jeanne » / « Jean ») soit essayé en entier d'abord.
            _objectNames = (objectNames ?? new List<string>())
                .OrderByDescending(n => n.Length)
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Point d'entrée principal
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reconnaît l'intention d'une phrase et retourne un JSON de commandes
        /// au même format que celui produit par le LLM, prêt à être passé à
        /// DeserializeCommand puis CommandToGraphOutputCommandAsync.
        /// Retourne null si aucune intention n'est reconnue.
        ///
        /// Les ORDRES EN CASCADE (« prends la commande de ce client et débarrasse cette
        /// table ») sont découpés en clauses (SplitClauses), reconnues chacune pour son
        /// compte : chaque commande garde les horodatages de SES mots, donc SES pointages,
        /// et ResolveCommands les exécute dans l'ordre — le serveur occupé par la première
        /// note les suivantes (Defer, le carnet du serveur).
        /// </summary>
        public string Recognize(Sentence sentence)
        {
            if (sentence == null || string.IsNullOrWhiteSpace(sentence.Text)) return null;

            var recognized = new List<Command>();
            foreach ((string clauseText, List<Word> clauseWords) in SplitClauses(sentence))
            {
                List<Command> built = Build(clauseText, clauseWords, null);
                if (built != null) recognized.AddRange(built);
            }
            if (recognized.Count == 0) return null;

            string produced = JsonConvert.SerializeObject(recognized, Formatting.Indented);
            Debug.Log($"[RuleBased] JSON produit :\n{produced}");
            return produced;
        }

        /// <summary>
        /// Découpe la phrase en CLAUSES de commande sur les connecteurs (« et », « puis »,
        /// « ensuite »). Un segment SANS verbe de commande n'ouvre pas de clause : il est
        /// rendu, connecteur compris, à la clause précédente — « prends la commande de Jean
        /// et de Florence » reste UNE commande à deux cibles (le « et » restitué garde
        /// l'union des prénoms), et « prépare une salade de fruits et une salade César »
        /// garde son chemin multi-recettes. Le test du verbe passe par DetectCommandType
        /// SANS son repli « plat nommé sans verbe » : une recette seule n'ouvre jamais de
        /// clause. Une seule clause au final : la phrase repart ENTIÈRE, texte original —
        /// rien ne change pour les phrases simples.
        /// </summary>
        private IEnumerable<(string Text, List<Word> Words)> SplitClauses(Sentence sentence)
        {
            List<Word> words = sentence.Words ?? new List<Word>();
            if (words.Count == 0)
            {
                // Phrase sans mots horodatés (texte synthétique) : rien à découper.
                yield return (sentence.Text, words);
                yield break;
            }

            var connectors = new HashSet<string> { "et", "puis", "ensuite", "and", "then" };

            // Segments bruts entre connecteurs, chacun mémorisant SON connecteur d'ouverture.
            // Un connecteur en tête de phrase (« Et débarrasse… ») reste un mot ordinaire.
            var segments = new List<(Word Connector, List<Word> Words)> { (null, new List<Word>()) };
            foreach (Word word in words)
            {
                string token = word.Text.ToLowerInvariant().Trim('.', ',', '!', '?', ';', ':');
                if (connectors.Contains(token) && segments[^1].Words.Count > 0)
                    segments.Add((word, new List<Word>()));
                else
                    segments[^1].Words.Add(word);
            }

            // Fusion : un segment sans verbe de commande retourne au précédent.
            var clauses = new List<List<Word>> { segments[0].Words };
            foreach ((Word connector, List<Word> segment) in segments.Skip(1))
            {
                if (segment.Count > 0 &&
                    DetectCommandType(TextOf(segment).ToLowerInvariant(), allowRecipeFallback: false) != null)
                {
                    clauses.Add(segment);
                }
                else
                {
                    clauses[^1].Add(connector);
                    clauses[^1].AddRange(segment);
                }
            }

            if (clauses.Count == 1)
            {
                yield return (sentence.Text, words);
                yield break;
            }

            Debug.Log($"[RuleBased] Cascade : {clauses.Count} clauses — " +
                      string.Join(" | ", clauses.Select(c => $"« {TextOf(c)} »")));
            foreach (List<Word> clause in clauses)
                yield return (TextOf(clause), clause);
        }

        /// <summary>Le texte d'une clause, reconstruit de ses mots horodatés.</summary>
        private static string TextOf(List<Word> clause)
            => string.Join(" ", clause.Select(w => w.Text));

        /// <summary>
        /// Le corps de la reconnaissance, qui rend la ou LES commandes : une phrase de
        /// préparation peut nommer plusieurs plats (« prépare une salade de fruits et une
        /// salade César ») — chacun devient sa propre PrepareCommand, exécutées dans
        /// l'ordre par ResolveCommands, notées et enchaînées par le cuisinier.
        /// </summary>
        /// <param name="forcedCommandType">
        /// Type imposé quand la phrase est une RÉPONSE à une question (« ce serveur-là 👆 » après
        /// « Quel serveur ? ») : dépourvue de verbe, aucun déclencheur ne peut la classer, alors
        /// que tout le reste — annotations, pointage, limite — s'en extrait normalement.
        /// Null en usage courant : le type se détecte.
        /// </param>
        private List<Command> Build(string rawText, List<Word> words, string forcedCommandType)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return null;

            string text = rawText.ToLowerInvariant().Trim();
            text = CorrectHomophones(text);
            words ??= new List<Word>();

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

            // 1. Détection du type de commande via les attributs [RuleBasedTriggers].
            // L'ajout à la sélection se détecte d'abord, sur le texte COMPLET : le mot
            // « sélection » vient d'être retiré du texte de détection, or c'est lui qui
            // distingue « ajoute les bananes à la sélection » d'un rangement (PutIn).
            string commandType = forcedCommandType
                                 ?? DetectAddToSelection(text)
                                 ?? DetectCommandType(commandText);
            if (commandType == null)
            {
                Debug.LogWarning($"[RuleBased] Aucune commande reconnue pour : \"{rawText}\"");
                return null;
            }

            Debug.Log($"[RuleBased] Commande détectée : {commandType} | phrase : \"{rawText}\"");

            // 2. Extraction des entités
            //
            // Les RECETTES d'abord, et le segment reconnu est RETIRÉ du texte avant la suite.
            // Sans cette consommation, « prépare une soupe de carottes » produirait à la fois
            // un RecipeParameter et une sélection de carottes : les noms de plats recouvrent
            // lexicalement des noms d'ingrédients (§6.4 du README). « Coupe les carottes pour
            // la soupe de carottes » reste correct : la recette consomme sa part, la première
            // occurrence demeure un ingrédient.
            List<string> recipes = FindRecipes(text, out string remainingText);
            string recipe = recipes.Count > 0 ? recipes[0] : null;

            // Les PRÉNOMS ensuite, consommés eux aussi : un prénom nomme UN objet du graphe —
            // la cible la plus spécifique qui soit — et rien de ce qu'il recouvre ne doit
            // produire d'autre filtre.
            List<RuleBasedAnnotation> names = FindNames(remainingText, words, out remainingText);

            List<RuleBasedAnnotation> annotations = FindAnnotations(remainingText, words);
            List<RuleBasedColor>      colors      = FindColors(remainingText, words);
            // Ablation (benchmark) : pointage désactivé → pas de déictiques (« ça » ne produit
            // plus de filtre Event), la résolution se fait à la voix seule.
            List<RuleBasedAnnotation> deictics    = MultimodalitySettings.PointingEnabled
                ? FindDeictics(text, words)
                : new List<RuleBasedAnnotation>();
            int  limit          = DetectLimit(text);
            // Intention au singulier (« la pomme ») : aucun marqueur pluriel NI nombre. Sert à la
            // désambiguïsation quand plusieurs cibles correspondent (cf. ResolveCommands).
            // Deux prénoms (« Jean et Florence ») = deux cibles VOULUES, pas une ambiguïté
            // à lever : sans cette garde, la désambiguïsation demanderait « laquelle ? ».
            bool singularIntent = !HasPluralMarker(text) && limit <= 1 && names.Count <= 1;
            // Une référence explicite à la sélection (« …sélectionnés », « la sélection »)
            // force la coréférence vers la sélection courante.
            bool hasCoreference = referencesSelection
                || (names.Count == 0 && annotations.Count == 0 && deictics.Count == 0 &&
                    HasCoreference(text));

            // Pour un AJOUT à la sélection, « la sélection » est la destination, pas la cible.
            // La coréférence forcée ci-dessus ferait ignorer les annotations
            // (BuildSelectionParameter ne construit QUE le filtre Coreference quand elle est
            // posée) : « ajoute les bananes à la sélection » re-sélectionnerait l'existant au
            // lieu d'y ajouter les bananes. Avec une cible explicite ou pointée, elle saute ;
            // sans cible (« rajoute-les »), elle reste le moyen de désigner quoi ajouter.
            if (commandType == "AddToSelectionCommand" && (annotations.Count > 0 || deictics.Count > 0))
                hasCoreference = false;

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

            // « tourne-le de 90 degrés » : le nombre est l'ANGLE de rotation, pas une limite de
            // sélection — sans ce cas particulier, DetectLimit sélectionnerait 90 objets. Même
            // traitement que ScaleToCommand (et même limitation : « tourne 3 pommes de 90° »
            // perd le compte, le premier nombre étant réinterprété).
            float angle = 0f;
            if (commandType == "RotateLeftCommand" || commandType == "RotateRightCommand")
            {
                angle = DetectAngle(text);
                if (angle > 0f && limit > 0)
                    limit = -1;
            }

            // Convention déictique : une référence pointée au singulier (« ça », « cette
            // banane ») cible UN objet → limit 1, comme l'enseigne le prompt LLM du système.
            // SANS pointage (« déplace la pomme »), on conserve -1 + SingularIntent : la
            // désambiguïsation (« laquelle ? ») doit pouvoir s'exécuter, un LIMIT 1 SPARQL
            // prendrait un objet arbitraire. « ces pommes » (pluriel) n'est pas concerné.
            if (deictics.Count > 0 && singularIntent && limit <= 0)
                limit = 1;

            Debug.Log(
                $"[RuleBased] Noms : [{string.Join(", ", names.Select(n => n.Value))}] | " +
                $"Annotations : [{string.Join(", ", annotations.Select(a => a.Value))}] | " +
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
                Names            = names,
                Annotations      = annotations,
                Colors           = colors,
                Deictics         = deictics,
                HasCoreference   = hasCoreference,
                Limit            = limit,
                ScaleFactor      = DetectScaleFactor(text),
                ScaleValue       = scaleValue,
                Angle            = angle,
                MagnitudeModifier = DetectMagnitudeModifier(text),
                // « les pommes ou les bananes » MAIS AUSSI « les pommes et les bananes » :
                // une énumération de deux TYPES d'objets est une union, quelle que soit la
                // conjonction. Le AND (intersection) entre deux annotations ne sélectionne
                // jamais rien — « sélectionne ce serveur et cette table » cherchait un objet
                // à la fois serveur ET table. Le AND par défaut reste pour les jonctions
                // annotation-couleur et annotation-pointage (« la pomme rouge », « ce bol »).
                HasDisjunction   = Regex.IsMatch(text, @"\b(et|ou|and|or)\b", RegexOptions.IgnoreCase),
                Order            = DetectOrder(text),
                SingularIntent   = singularIntent,
                Recipe           = recipe
            };

            Command cmd = CreateCommand(commandType);
            cmd.Parameters = cmd.BuildRuleBasedParameters(ctx);
            if (cmd.Parameters == null)
                return null;

            var commands = new List<Command> { cmd };

            // « Prépare une salade de fruits ET une salade César » : chaque plat nommé
            // au-delà du premier devient SA PROPRE PrepareCommand — le cuisinier note et
            // enchaîne (Cook.Prepare). Seule la préparation est concernée : les autres
            // commandes ne consomment qu'une recette (le service nommé, par exemple, porte
            // UN plat à la fois).
            if (commandType == "PrepareCommand")
                foreach (string extra in recipes.Skip(1))
                {
                    Command another = CreateCommand("PrepareCommand");
                    another.Parameters = another.BuildRuleBasedParameters(new RuleBasedContext
                    {
                        Text             = text,
                        Words            = words,
                        PointerName      = _pointerName,
                        MovePointDelayMs = _movePointDelayMs,
                        Recipe           = extra
                    });
                    if (another.Parameters != null) commands.Add(another);
                }

            return commands;
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

            // Réponse de type CIBLE (« ce serveur-là 👆 » après « Quel serveur ? »). Ce n'est pas
            // un paramètre qui manque à la commande en attente — elle a bien sa sélection — mais
            // un RÔLE : elle n'y trouve pas de serveur. La phrase-réponse n'ayant pas de verbe,
            // on relance la reconnaissance complète en lui imposant le type de la commande en
            // attente, puis on demande l'union avec la sélection courante : la cible déjà trouvée
            // y a été laissée par Execute, et la réponse doit s'y AJOUTER, pas la remplacer.
            if (!filled && pending.ExpectsTargetAnswer)
            {
                Command answer = Build(sentence.Text, sentence.Words, pending.Type)?.FirstOrDefault();
                List<SelectionParameter> selections =
                    answer?.Parameters?.OfType<SelectionParameter>().ToList() ?? new List<SelectionParameter>();

                if (selections.Count > 0)
                {
                    foreach (SelectionParameter selection in selections)
                        selection.UnionWithSelection = true;

                    string answered = JsonConvert.SerializeObject(new List<Command> { answer }, Formatting.Indented);
                    Debug.Log($"[RuleBased] {pending.Type} complétée par une cible :\n{answered}");
                    return answered;
                }
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

        /// <summary>
        /// Pré-vérification : verbe d'ajout + mention de la sélection → AddToSelectionCommand.
        /// Elle se joue sur le texte COMPLET, avant DetectCommandType : Recognize retire le mot
        /// « sélection » du texte de détection (il force la coréférence pour « les objets
        /// sélectionnés »), donc aucun déclencheur multi-mots (« ajoute à la sélection ») ne
        /// peut le voir. Une préposition de contenant garde la lecture rangement : « ajoute les
        /// tomates sélectionnées dans le bol » reste un PutInCommand.
        /// </summary>
        private static string DetectAddToSelection(string text)
        {
            string normalized = FrenchStemmer.NormalizeAccents(text);
            string addVerbs = IsFrench
                ? @"\b(ajoute|ajoutez|ajouter|rajoute|rajoutez|rajouter)\b"
                : @"\b(add|adds)\b";
            // Le NOM « sélection(s) » seulement : ni le verbe « sélectionne » ni le participe
            // « sélectionné » (dans leurs formes normalisées, « selection » est suivi d'une
            // lettre — pas de frontière de mot).
            if (!Regex.IsMatch(normalized, addVerbs) || !Regex.IsMatch(normalized, @"\bselections?\b"))
                return null;

            string[] containerPrepositions = IsFrench
                ? new[] { "dans", "sur", "dedans" }
                : new[] { "in", "into", "on", "onto" };
            if (containerPrepositions.Any(p => Regex.IsMatch(normalized, $@"\b{p}\b")))
                return null;

            return "AddToSelectionCommand";
        }

        /// <param name="allowRecipeFallback">
        /// Faux pour le TEST DE COUPE des clauses (SplitClauses) : un plat nommé sans verbe
        /// n'ouvre pas de clause — « et une salade César » prolonge la préparation en cours,
        /// il ne la suit pas.
        /// </param>
        private string DetectCommandType(string text, bool allowRecipeFallback = true)
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

            // Pré-vérification : verbe de rangement + préposition non-contiguë → PutInCommand.
            // Même raison que ci-dessus : dans « mets les pommes dans l'assiette », le verbe et
            // la préposition sont séparés par la cible, donc le trigger multi-mots « mets dans »
            // ne peut pas se déclencher. Placé APRÈS MoveCommand : les deux jeux de mots ne se
            // recouvrent pas (« ici / là-bas » contre « dans / sur »), l'ordre est donc sans
            // conséquence, mais on garde la destination déictique prioritaire par principe.
            // Les formes en -ez y sont : Whisper transcrit volontiers au vouvoiement
            // (« Déposez les citrouilles dans ce bol »), et « déposer » manquait — la phrase
            // retombait alors sur le déclencheur « dépose » de ReleaseCommand, qui construisait
            // UNE sélection « citrouille ET pointée » au lieu de couper au pivot « dans » :
            // intersection vide, « aucun objet correspondant ».
            // « rajoute » y est aussi : sans lui, « rajoute une banane dans le bol » passerait
            // au tour des déclencheurs, où « rajoute » appartient à AddToSelectionCommand.
            string putVerbs = IsFrench
                ? @"\b(mets|mettez|met|mettre|pose|posez|poser|depose|deposez|deposer|range|rangez|ranger|ajoute|ajoutez|ajouter|rajoute|rajoutez|rajouter|verse|versez|verser)\b"
                : @"\b(put|place|add|pour|drop)\b";
            string[] containerPrepositions = IsFrench
                ? new[] { "dans", "sur", "dedans" }
                : new[] { "in", "into", "on", "onto" };
            if (Regex.IsMatch(normalizedText, putVerbs) &&
                containerPrepositions.Any(p => Regex.IsMatch(normalizedText, $@"\b{p}\b", RegexOptions.IgnoreCase)))
            {
                return "PutInCommand";
            }

            // Pré-vérification : verbe de fabrication générique + nom de recette → PrepareCommand.
            // « fais / faire / make » sont trop généraux pour être des déclencheurs : « make it
            // red » est un ColorizeCommand, « make a soup » un PrepareCommand, et les deux
            // déclencheurs « make » avaient la même longueur — la boucle ordonnée ci-dessous
            // tranchait donc selon l'ordre de réflexion des types, c'est-à-dire au hasard.
            // C'est la PRÉSENCE d'un nom de recette qui lève l'ambiguïté, pas le verbe ; ces
            // trois verbes ont donc été retirés des [RuleBasedTriggers] de PrepareCommand.
            string makeVerbs = IsFrench ? @"\b(fais|faire|prepare|preparer)\b" : @"\b(make|prepare|cook)\b";
            if (Regex.IsMatch(normalizedText, makeVerbs) && FindRecipe(text, out _) != null)
                return "PrepareCommand";

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

            // Une phrase qui NOMME UN PLAT et ne correspond à aucune commande est un ordre de
            // préparation. Whisper mange volontiers le verbe — « Prépare une soupe » est sorti
            // « Par une soupe », « Répare une soupe », « Et par une soupe » dans la même
            // session — et c'est aussi la forme naturelle de la réponse à « Laquelle : soupe
            // de carottes ou soupe de citrouille ? » : « Soupe de carottes. » Quand le verbe
            // manque, le plat suffit à dire l'intention. Les QUESTIONS sont exclues :
            // « est-ce que c'est une salade de fruits ? » interroge sur un plat, elle n'en
            // commande pas.
            bool question = text.Contains("?") || Regex.IsMatch(normalizedText, @"\best[ -]ce\b");
            if (allowRecipeFallback && !question && FindRecipe(text, out _) != null)
            {
                // « C'est la salade César » : Whisper entend « Sers » /sɛʁ/ et écrit son
                // homophone « C'est » — le verbe de service disparaît de la transcription,
                // jamais de l'intention (mesuré en démo : « Sers la salade César à
                // Florence » → « C'est à la salade César à Florence », et le repli d'alors
                // faisait REFAIRE le plat au lieu de le servir). Un plat nommé introduit par
                // « c'est » est donc un ordre de SERVICE ; les vraies questions (« est-ce
                // que c'est prêt ? ») sont déjà écartées ci-dessus, et « c'est prêt » sans
                // plat nommé n'atteint jamais ce repli.
                if (Regex.IsMatch(normalizedText, @"\bc\s?['’]?\s?est\b", RegexOptions.IgnoreCase))
                    return "ServeCommand";

                return "PrepareCommand";
            }

            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Extraction des entités
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Cherche un nom de recette, du libellé le plus long au plus court, et retire du texte
        /// le segment reconnu.
        ///
        /// « Le plus précis » se lit « le plus long », convention déjà en vigueur pour les
        /// déclencheurs. La consommation du segment est ce qui empêche « soupe de carottes »
        /// d'être aussi lue comme une carotte.
        /// </summary>
        /// <param name="remainingText">Le texte privé du nom de recette.</param>
        /// <returns>Le nom préfixé de la recette, ou null si aucune n'est nommée.</returns>
        /// <summary>
        /// TOUS les plats nommés dans la phrase, dans l'ordre de détection, chacun consommé
        /// du texte avant de chercher le suivant. La garde sur le texte inchangé pare au
        /// retrait qui ne mordrait pas (graphie inattendue du libellé) : mieux vaut perdre
        /// un doublon que boucler sans fin sur la même recette.
        /// </summary>
        private List<string> FindRecipes(string text, out string remainingText)
        {
            var recipes = new List<string>();
            remainingText = text;

            while (true)
            {
                string found = FindRecipe(remainingText, out string next);
                if (found == null) break;
                recipes.Add(found);
                if (next == remainingText) break;
                remainingText = next;
            }

            return recipes;
        }

        private string FindRecipe(string text, out string remainingText)
        {
            remainingText = text;

            foreach (RecipeVocabulary.Recipe recipe in _recipes)
            {
                string label = FrenchStemmer.NormalizeAccents(recipe.Label.ToLowerInvariant());
                if (!Regex.IsMatch(FrenchStemmer.NormalizeAccents(text),
                                   $@"\b{PluralTolerantPattern(label)}\b", RegexOptions.IgnoreCase))
                    continue;

                remainingText = Regex.Replace(
                    text, $@"\b{PluralTolerantPattern(recipe.Label)}\b", " ", RegexOptions.IgnoreCase);

                Debug.Log($"[RuleBased] Recette reconnue : {recipe.Label} → {recipe.Uri}" +
                          (recipe.IsConcrete ? "" : " (famille — sous-spécifiée)"));
                return recipe.Uri;
            }
            return null;
        }

        /// <summary>
        /// Les types d'annotation nommés dans la phrase.
        ///
        /// Chaque libellé reconnu est RETIRÉ du texte de travail, comme le fait FindRecipe pour
        /// les recettes. Sans cette consommation, les libellés qui en contiennent d'autres
        /// produisent une annotation parasite : « pomme de terre » vaut aussi « pomme », et
        /// « planche à découper » vaut aussi « planche ». Combinée au tri par longueur
        /// décroissante fait dans le constructeur, elle garantit que le libellé le plus précis
        /// gagne — la même convention que partout ailleurs dans ce reconnaisseur.
        /// </summary>
        private List<RuleBasedAnnotation> FindAnnotations(string text, List<Word> words)
        {
            var result = new List<RuleBasedAnnotation>();
            string remaining = text;

            foreach (string annotation in _annotationTypes)
            {
                string lower = annotation.ToLowerInvariant();
                foreach (string form in GetFrenchForms(lower))
                {
                    if (ContainsPhrase(remaining, form))
                    {
                        result.Add(new RuleBasedAnnotation
                        {
                            Value     = annotation,
                            // L'horodatage se cherche dans la phrase d'ORIGINE : les mots
                            // consommés en sont absents.
                            Timestamp = GetWordTimestamp(words, form, useStartedAt: false)
                        });
                        remaining = ConsumeFirst(remaining, form);
                        break; // une seule occurrence par annotation
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Les objets de la scène NOMMÉS dans la phrase (« Sers Florence ») — les prénoms
        /// des clients, injectés par le contrôleur depuis les objets sémantisés. La valeur
        /// retenue est le nom CANONIQUE (celui du GameObject, donc du rdfs:label écrit par
        /// SVEN) : c'est ce littéral que le filtre « Name » de la sélection compare.
        ///
        /// Chaque prénom reconnu est RETIRÉ du texte de travail, comme les recettes et les
        /// annotations. La correspondance tolère les consonnes simples ou doublées
        /// (« Floriane » ↔ « Florianne ») : Whisper orthographie les prénoms à l'oreille,
        /// et un prénom n'a pas de graphie ontologique à laquelle se fier.
        /// </summary>
        private List<RuleBasedAnnotation> FindNames(string text, List<Word> words, out string remaining)
        {
            var result = new List<RuleBasedAnnotation>();
            remaining = text;

            foreach (string objectName in _objectNames)
            {
                var pattern = new Regex($@"\b{DoubledLetterTolerantPattern(objectName)}\b",
                    RegexOptions.IgnoreCase);
                Match match = pattern.Match(FrenchStemmer.NormalizeAccents(remaining));
                if (!match.Success) continue;

                result.Add(new RuleBasedAnnotation
                {
                    Value     = objectName,
                    Timestamp = GetWordTimestamp(words, match.Value, useStartedAt: false)
                });
                remaining = pattern.Replace(remaining, " ", 1);
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
            for (int i = 0; i < tokens.Length; i++)
            {
                // « un peu » : « un » appartient à la locution d'intensité (cf.
                // DetectMagnitudeModifier), ce n'est pas un compte d'objets — sans cette garde,
                // « agrandis-les un peu » limiterait la sélection à 1 objet.
                if (i + 1 < tokens.Length &&
                    (tokens[i].Equals("un", StringComparison.OrdinalIgnoreCase) ||
                     tokens[i].Equals("une", StringComparison.OrdinalIgnoreCase)) &&
                    tokens[i + 1].Equals("peu", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    continue;
                }
                if (Numbers.TryGetValue(tokens[i], out int num))
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
        /// Angle de rotation explicite en degrés : « de 90 degrés », « de 90° », « d'un quart
        /// de tour » (90°), « d'un demi-tour » (180°), en français et en anglais ; 0 si non
        /// spécifié (la commande applique alors son incrément par défaut, éventuellement modulé
        /// par <see cref="DetectMagnitudeModifier"/>).
        /// </summary>
        private static float DetectAngle(string text)
        {
            string n = FrenchStemmer.NormalizeAccents(text);
            // Fractions de tour d'abord : pas de chiffre à extraire.
            if (Regex.IsMatch(n, @"\bquart de tour\b|\bquarter[- ]?turn\b")) return 90f;
            if (Regex.IsMatch(n, @"\bdemi[- ]?tour\b|\bhalf[- ]?turn\b")) return 180f;
            // « de 90 degrés » / « 90° » / "by 90 degrees" — décimales acceptées (« 22,5 degrés »).
            Match m = Regex.Match(n, @"\b(\d+(?:[.,]\d+)?)\s*(?:°|degres?\b|degrees?\b)");
            if (m.Success && float.TryParse(m.Groups[1].Value.Replace(',', '.'),
                                            NumberStyles.Float, CultureInfo.InvariantCulture, out float degrees))
                return degrees;
            return 0f;
        }

        /// <summary>
        /// Coefficient d'intensité des adverbes graduables : « un peu »/« légèrement »/
        /// « a bit »/« slightly » → 0.5 ; « beaucoup »/« fortement »/« a lot »/« much » → 2 ;
        /// 1 sinon. Coefficients arbitraires (moitié/double) mais cohérents entre rotation et
        /// échelle : ils s'appliquent à l'ÉCART de la transformation à l'identité (45° →
        /// 22,5°/90° ; ×1.1 → ×1.05/×1.2), jamais à la valeur brute.
        /// </summary>
        private static float DetectMagnitudeModifier(string text)
        {
            string n = FrenchStemmer.NormalizeAccents(text);
            if (Regex.IsMatch(n, @"\bun peu\b|\blegerement\b|\ba bit\b|\bslightly\b")) return 0.5f;
            if (Regex.IsMatch(n, @"\bbeaucoup\b|\bfortement\b|\ba lot\b|\bmuch\b")) return 2f;
            return 1f;
        }

        /// <summary>
        /// Tri ordinal superlatif : « plus petit(e)(s) » / "smallest" → taille croissante ;
        /// « plus grand(e)(s) »/« plus gros(se)(s) » / "biggest"/"largest" → décroissante ;
        /// null sinon. Combiné à la limite (« les 3 plus petites pommes » → limit 3 + tri),
        /// il produit le critère « size » consommé par SelectionParameter.Order en SPARQL.
        /// </summary>
        private static Order DetectOrder(string text)
        {
            string n = FrenchStemmer.NormalizeAccents(text);
            if (Regex.IsMatch(n, @"\bplus\s+petite?s?\b|\bsmallest\b"))
                return new Order { Criterias = new List<Criteria> { new() { Type = "size", Desc = false } } };
            if (Regex.IsMatch(n, @"\bplus\s+(grande?s?|grosse?s?)\b|\bbiggest\b|\blargest\b"))
                return new Order { Criterias = new List<Criteria> { new() { Type = "size", Desc = true } } };
            return null;
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
        /// Motif regex de la phrase où chaque LIGATURE accepte aussi sa graphie à deux
        /// lettres (« bœuf » ↔ « boeuf ») : les libellés de l'ontologie portent la ligature,
        /// le STT l'écrit en toutes lettres, et la consommation doit opérer sur le texte
        /// D'ORIGINE (les mots restants gardent leur graphie pour les horodatages).
        /// </summary>
        private static string LigatureTolerantPattern(string phrase)
            => Regex.Escape(phrase)
                .Replace("œ", "(?:œ|oe)").Replace("Œ", "(?:Œ|OE)")
                .Replace("æ", "(?:æ|ae)").Replace("Æ", "(?:Æ|AE)");

        /// <summary>
        /// Motif d'un libellé dont CHAQUE mot tolère un « s » final en plus ou en moins.
        ///
        /// Whisper accorde à l'oreille : « soupe de carottes » sort « soupe de carotte » au
        /// gré de la prosodie, et l'accord exact faisait retomber la phrase sur la FAMILLE
        /// (« soupe ») — le joueur nommait la bonne recette et s'entendait demander laquelle.
        /// La tolérance est bornée au « s » final : rien d'autre ne varie à l'oral sur un nom
        /// de plat, et élargir davantage ferait se recouvrir des libellés distincts.
        /// </summary>
        private static string PluralTolerantPattern(string phrase)
        {
            IEnumerable<string> words = phrase
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => LigatureTolerantPattern(word.TrimEnd('s', 'S')) + "s?");
            return string.Join(@"\s+", words);
        }

        /// <summary>
        /// Motif d'un prénom où chaque lettre accepte d'être simple ou doublée
        /// (« Florianne » ↔ « Floriane ») : les doublons du prénom sont d'abord fusionnés,
        /// puis chaque lettre émet « x{1,2} ». Réservé aux prénoms — le vocabulaire
        /// ontologique a une graphie de référence, cette tolérance y sur-couvrirait.
        /// </summary>
        private static string DoubledLetterTolerantPattern(string name)
        {
            var pattern = new System.Text.StringBuilder();
            char previous = '\0';
            foreach (char letter in name)
            {
                if (char.ToLowerInvariant(letter) == previous) continue;
                previous = char.ToLowerInvariant(letter);
                pattern.Append(Regex.Escape(letter.ToString())).Append("{1,2}");
            }
            return pattern.ToString();
        }

        /// <summary>
        /// Retire du texte la PREMIÈRE occurrence de la phrase. Une seule, pour que « la pomme
        /// et la pomme de terre » garde sa pomme après que la patate a consommé la sienne.
        /// </summary>
        private static string ConsumeFirst(string text, string phrase)
            => new Regex($@"\b{LigatureTolerantPattern(phrase)}\b", RegexOptions.IgnoreCase)
                .Replace(text, " ", 1);

        /// <summary>
        /// Vérifie si le texte contient une phrase, AVEC frontière de mots — multi-mots
        /// compris. Le Contains brut d'origine faisait gagner « sélectionne tout » À
        /// L'INTÉRIEUR de « deselectionne tout » (Whisper écrit volontiers sans accents) :
        /// « désélectionne tout » sélectionnait les 46 objets de la scène au lieu de vider
        /// la sélection. Même règle que ContainsCommandTrigger côté contrôleur.
        /// Les deux côtés sont normalisés (accents ET ligatures) : « Bœuf » du vocabulaire
        /// doit reconnaître le « boeuf » que Whisper écrit.
        /// </summary>
        private bool ContainsPhrase(string text, string phrase)
            => Regex.IsMatch(FrenchStemmer.NormalizeAccents(text),
                $@"\b{Regex.Escape(FrenchStemmer.NormalizeAccents(phrase))}\b",
                RegexOptions.IgnoreCase);

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

                // Accord féminin régulier (« pomme verte », « bananes vertes ») : sans lui, une
                // couleur accordée au féminin passait inaperçue. Les formes déjà en -e (rouge,
                // jaune, rose) ont un féminin identique ; les irréguliers (blanc → blanche)
                // restent non couverts.
                if (!baseForm.EndsWith("e"))
                {
                    forms.Add(baseForm + "e");   // vert → verte
                    forms.Add(baseForm + "es");  // vert → vertes
                }
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
