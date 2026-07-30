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
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using VDS.RDF;
using VDS.RDF.Parsing;
using Pointer = Sven.Context.Pointer;

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

        [BoxGroup("LLM Settings"), ShowIf("IsLlmModeLocal"), SerializeField, Tooltip("Clé API optionnelle envoyée en « Authorization: Bearer » au serveur LLM local — requise si celui-ci est derrière un proxy authentifié (cf. README § Serveur). Laisser vide pour un serveur sans authentification, ou pour utiliser la variable d'environnement LOCAL_LLM_API_KEY (recommandé : ce champ est sérialisé dans la scène, donc committé avec elle).")]
        private string _localLlmApiKey;

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

        /// <summary>
        /// Clé API optionnelle du serveur LLM local : champ Inspector si renseigné, sinon
        /// variable d'environnement LOCAL_LLM_API_KEY. Vide → aucun header Authorization
        /// (serveur local sans authentification).
        /// </summary>
        private string LocalLlmApiKey =>
            !string.IsNullOrWhiteSpace(_localLlmApiKey)
                ? _localLlmApiKey
                : Environment.GetEnvironmentVariable("LOCAL_LLM_API_KEY");

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

        // Le modèle du prompt système et sa compilation vivent dans LlmIntentService
        // (classe sans dépendance UnityEngine, partagée avec le harnais EditMode).

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
            _cachedSystemPrompt = LlmIntentService.BuildSystemPrompt(
                _annotationTypesString,
                _availableColorsString,
                _cameraNamesString,
                _pointerNamesString,
                _pointerDeicticsString,
                _availableCommandsString);

            // Version locale : on retire la section EXEMPLES pour réduire la taille du prompt
            // (~6 500 → ~3 000 tokens) et permettre de fonctionner avec n_ctx = 4 096.
            _cachedSystemPromptLocal = LlmIntentService.TrimExamplesSection(_cachedSystemPrompt);

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
        /// La construction de la requête et l'appel HTTP vivent dans LlmIntentService
        /// (classe sans dépendance UnityEngine, partagée avec le harnais EditMode).
        /// </summary>
        private async Task<string> CallLlmApiAsync(Sentence sentence, string model)
        {
            await InitializeVocabulariesAsync();

            string finalSystemPrompt = _llmService == LlmService.Local
                ? _cachedSystemPromptLocal
                : _cachedSystemPrompt;

            string userContent = JsonConvert.SerializeObject(new { sentence.Text, sentence.Words });
            Debug.Log(userContent + "\n\n" + finalSystemPrompt);

            string endpointBaseUrl;
            string apiKey;
            if (_llmService == LlmService.OpenAI)
            {
                apiKey = OpenAiApiKey;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    Debug.LogError("[LLM] Clé API OpenAI absente : définir la variable d'environnement OPENAI_API_KEY (recommandé) ou le champ Inspector.");
                    return null;
                }
                endpointBaseUrl = null; // null → API OpenAI
            }
            else // LlmService.Local
            {
                if (string.IsNullOrWhiteSpace(_localLlmUrl))
                {
                    Debug.LogError("[LLM] Local LLM URL is not set. Please set it in the inspector.");
                    return null;
                }
                endpointBaseUrl = _localLlmUrl;
                apiKey = LocalLlmApiKey; // vide → aucun header Authorization
            }

            LlmIntentService.CallResult result = await LlmIntentService.CallChatCompletionsAsync(
                _httpClient, endpointBaseUrl, apiKey, model, finalSystemPrompt, userContent,
                // json_object est supporté par OpenAI gpt-4o/mini mais pas par tous les serveurs
                // locaux ; on l'omet côté local pour éviter les erreurs de compatibilité.
                jsonObjectFormat: _llmService == LlmService.OpenAI);

            if (result.Error != null)
            {
                Debug.LogError($"[LLM] Appel {model} échoué : {result.Error}");
                return null;
            }
            if (result.PromptTokens > 0 || result.CompletionTokens > 0)
                Debug.Log($"[LLM] Token Usage ({model}): Prompt={result.PromptTokens}, " +
                          $"Completion={result.CompletionTokens}, Total={result.PromptTokens + result.CompletionTokens}");
            return result.Content;
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