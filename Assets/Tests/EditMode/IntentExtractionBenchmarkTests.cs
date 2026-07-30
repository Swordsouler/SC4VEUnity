using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Sc4ve.Multimodality;
using Sc4ve.Multimodality.Intent;
using Sc4ve.Multimodality.Intent.RuleBased;
using Sc4ve.Voice;
using Sven.Content;
using Sven.Context;
using Sven.GraphManagement;
using Sven.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Sc4ve.Tests.EditMode
{
    /// <summary>
    /// Harnais d'évaluation de l'extraction d'intention : rejoue les 35 cas annotés de
    /// sc4ve_test_cases.json sur le mode règles (toujours) et le mode LLM (test [Explicit],
    /// configuré par variables d'environnement), puis produit les métriques du tableau de
    /// résultats de la thèse : exactitude du type, F-mesure des paramètres (agrégée sur le
    /// jeu, pas moyennée par cas), taux de clarification (légitime / à tort), latence médiane.
    ///
    /// Conditions expérimentales (rappelées dans chaque rapport) :
    /// - movePointDelayMs = 0 : le jeu annoté attend le EndedAt exact du terme de destination ;
    ///   le délai de stabilisation du geste (300 ms à l'exécution) est un réglage d'exécution,
    ///   pas d'extraction.
    /// - le contexte de dialogue (context.history) n'est pas injecté : il n'influence pas le
    ///   JSON extrait (la résolution de la coréférence est postérieure à l'extraction).
    /// - l'issue « no_match » exige une scène : au niveau extraction, elle est comptée comme
    ///   « executed » (la commande est bien formée, c'est la résolution qui échoue).
    /// - mode LLM : un seul appel par cas (pas de cascade de validation fast→precise du
    ///   contrôleur), afin d'attribuer chaque mesure à un modèle unique.
    /// </summary>
    public class IntentExtractionBenchmarkTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Modèle du jeu de cas
        // ─────────────────────────────────────────────────────────────────────

        private class BenchCase
        {
            public string Id;
            public string Lang;
            public string Category;
            public string Text;
            public List<Word> Words;
            public string ExpectedOutcome;
            public string ExpectedRawJson; // avec placeholder {pointerTerm}, substitué par locale
            public bool AblationSensitive;
        }

        private class CaseRun
        {
            public string ProducedJson;
            public double TotalMs;
            public double? HttpMs;
            public string Error;
        }

        private class CaseResult
        {
            public BenchCase Case;
            public CaseRun Run;
            public string PredictedOutcome;
            public bool OutcomeOk;
            public bool TypeOk;
            public int Tp, Fp, Fn;                   // stricte (type, valeur, horodatage, limit, order)
            public int TpNoTs, FpNoTs, FnNoTs;       // sans horodatage
            public int TpRelax, FpRelax, FnRelax;    // sans horodatage + conjonctions non ordonnées
            public List<string> Notes = new();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Configuration par locale (vocabulaires ontologiques + recognizer + prompt)
        // ─────────────────────────────────────────────────────────────────────

        private class LocaleSetup
        {
            public RuleBasedIntentRecognizer Recognizer;
            public string PointerTerm;
            public string SystemPromptFull;
        }

        private readonly Dictionary<string, LocaleSetup> _localeSetups = new();
        private string _currentLang;
        private Language _previousLanguage;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _previousLanguage = UserData.Language;
            // Application.streamingAssetsPath n'est lisible que sur le thread principal :
            // mise en cache avant les Task.Run des initialisations de vocabulaires.
            SvenSettings.CacheMainThreadPaths();
            // Enregistre le préfixe vide du graphe partagé : la sérialisation du message
            // utilisateur LLM (Words → propriété Event.UriNode) le requiert hors Play mode.
            try
            {
                GraphManager.SetBaseUri(SvenSettings.BaseUri);
                GraphManager.SetNamespace("", SvenSettings.BaseUri);
            }
            catch { /* déjà enregistré */ }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => UserData.Language = _previousLanguage;

        // Bascule la locale active et resynchronise les vocabulaires statiques mis en cache
        // par locale (CommandVocabulary, ClarificationVocabulary). EditModeSync évite
        // l'interblocage du contexte de synchronisation Unity sur l'attente bloquante.
        private void EnsureLocale(string lang)
        {
            if (_currentLang == lang) return;
            UserData.Language = lang == "fr" ? Language.French : Language.English;
            EditModeSync.RunSync(async () =>
            {
                await CommandVocabulary.InitializeAsync();
                await ClarificationVocabulary.InitializeAsync();
            });
            _currentLang = lang;
        }

        private LocaleSetup GetLocaleSetup(string lang)
        {
            EnsureLocale(lang);
            if (_localeSetups.TryGetValue(lang, out LocaleSetup cached)) return cached;

            Language language = lang == "fr" ? Language.French : Language.English;
            string locale = UserData.Locale;

            LocaleSetup setup = EditModeSync.RunSync(async () =>
            {
                // Vocabulaires depuis les ontologies de la scène de démonstration
                // (sven-fruits.ttl, color.ttl…) — mêmes sources que MultimodalityController.
                List<string> annotations = await ISemanticAnnotation.GetAllAvailableTypes(locale);
                List<string> colors      = await ColorParameter.GetAllAvailableColors(language);
                List<string> pointers    = await Sven.Context.Pointer.GetAllAvailableNames(locale);
                List<string> cameras     = await PointOfView.GetAllAvailableNames(locale);

                var s = new LocaleSetup { PointerTerm = string.Join(", ", pointers) };

                List<string> deictics = CommandVocabulary.Deictics;
                s.Recognizer = new RuleBasedIntentRecognizer(
                    annotations,
                    colors,
                    deictics.Select(d => d.Trim('\'')).ToList(),
                    s.PointerTerm,
                    string.Join(", ", cameras),
                    // 0 : le jeu annoté attend le EndedAt exact du terme de destination
                    // (cf. conditions expérimentales en tête de classe).
                    movePointDelayMs: 0);

                // Prompt système compilé exactement comme dans le contrôleur (mêmes substitutions).
                s.SystemPromptFull = LlmIntentService.BuildSystemPrompt(
                    string.Join(", ", annotations),
                    string.Join(", ", colors),
                    string.Join(", ", cameras),
                    s.PointerTerm,
                    string.Join(", ", deictics),
                    CommandVocabulary.CommandsDescription);
                return s;
            });

            _localeSetups[lang] = setup;
            return setup;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Chargement du jeu de cas
        // ─────────────────────────────────────────────────────────────────────

        private static string CasesPath =>
            Environment.GetEnvironmentVariable("SC4VE_BENCH_CASES")
            ?? Path.Combine(Application.dataPath, "Tests", "EditMode", "sc4ve_test_cases.json");

        private static List<BenchCase> LoadCases()
        {
            string path = CasesPath;
            Assert.IsTrue(File.Exists(path), $"Jeu de cas introuvable : {path}");
            JObject root = JObject.Parse(File.ReadAllText(path));

            var cases = new List<BenchCase>();
            foreach (JObject c in (JArray)root["cases"])
            {
                var words = new List<Word>();
                foreach (JObject w in (JArray)c["input"]["Words"])
                    words.Add(new Word((string)w["Text"], (DateTime)w["StartedAt"], (DateTime)w["EndedAt"]));

                cases.Add(new BenchCase
                {
                    Id                = (string)c["id"],
                    Lang              = (string)c["lang"],
                    Category          = (string)c["category"],
                    Text              = (string)c["input"]["Text"],
                    Words             = words,
                    ExpectedOutcome   = (string)c["expected_outcome"],
                    ExpectedRawJson   = c["expected_commands"].ToString(Formatting.None),
                    AblationSensitive = (bool?)c["ablation_sensitive"] ?? false
                });
            }

            int declaredTotal = (int)root["counts"]["total"];
            Assert.AreEqual(declaredTotal, cases.Count,
                "Le nombre de cas chargés ne correspond pas à counts.total du fichier.");
            return cases;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Tests
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void RuleBased_Benchmark()
        {
            RunBenchmark(
                "rulebased",
                (benchCase, setup) =>
                {
                    // Le pointage doit être actif (les cas d'ablation le supposent).
                    MultimodalitySettings.PointingEnabled = true;
                    var sentence = new Sentence(benchCase.Text, benchCase.Words);
                    var stopwatch = Stopwatch.StartNew();
                    string json = setup.Recognizer.Recognize(sentence);
                    stopwatch.Stop();
                    return new CaseRun { ProducedJson = json, TotalMs = stopwatch.Elapsed.TotalMilliseconds };
                },
                "mode règles (RuleBasedIntentRecognizer), sans réseau ; latence = Recognize() seul");
        }

        // Timeout NUnit relevé (défaut : 3 min) : 35 appels × jusqu'à 120 s de timeout HTTP
        // chacun — un modèle local lent (~30 s/cas observé avec Qwen3-4B sur prompt ~3 200
        // tokens) dépasse largement le défaut alors que le run est valide.
        [Test, Timeout(7_200_000),
         Explicit("Appelle un LLM (API OpenAI ou serveur local) — configurer SC4VE_BENCH_LLM_MODEL, " +
                  "SC4VE_BENCH_LLM_URL (vide → OpenAI), OPENAI_API_KEY / LOCAL_LLM_API_KEY.")]
        public void Llm_Benchmark()
        {
            string model = Environment.GetEnvironmentVariable("SC4VE_BENCH_LLM_MODEL");
            if (string.IsNullOrWhiteSpace(model))
                Assert.Ignore("SC4VE_BENCH_LLM_MODEL non défini — benchmark LLM ignoré. Configurations de la " +
                              "thèse : gpt-4o-mini / gpt-4o (API OpenAI), Qwen3-4B-Instruct / Mistral-Nemo-12B " +
                              "(serveur local via SC4VE_BENCH_LLM_URL, ex: http://localhost:1234/v1).");

            string url   = Environment.GetEnvironmentVariable("SC4VE_BENCH_LLM_URL");
            bool   local = !string.IsNullOrWhiteSpace(url);
            // Jamais de clé dans le code ni dans une scène : variables d'environnement uniquement.
            string apiKey = local
                ? Environment.GetEnvironmentVariable("LOCAL_LLM_API_KEY")
                : Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!local && string.IsNullOrWhiteSpace(apiKey))
                Assert.Ignore("OPENAI_API_KEY absente — benchmark LLM OpenAI ignoré.");

            // Même politique que le contrôleur : prompt allégé (sans exemples) pour les serveurs
            // locaux à fenêtre limitée. Surchargable par SC4VE_BENCH_TRIM_EXAMPLES=0|1.
            string trimEnv = Environment.GetEnvironmentVariable("SC4VE_BENCH_TRIM_EXAMPLES");
            bool trimExamples = trimEnv == null ? local : trimEnv == "1";

            string label = Environment.GetEnvironmentVariable("SC4VE_BENCH_LLM_LABEL") ?? model;
            string config = "llm_" + Sanitize(label);

            string promptCondition = trimExamples
                ? "ALLÉGÉ (TrimExamplesSection : les exemples sont retirés, ~6 500 → ~3 200 tokens — " +
                  "condition expérimentale différente des modèles évalués avec le prompt complet)"
                : "complet";
            string conditions =
                $"mode LLM, modèle « {model} » via {(local ? url : "API OpenAI")} ; " +
                $"prompt {promptCondition} ; " +
                "un seul appel par cas (pas de cascade fast→precise) ; " +
                "latence totale = appel HTTP + post-traitement, colonne http_ms = aller-retour HTTP seul " +
                "(réseau + inférence, non séparables côté client)";

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

            RunBenchmark(
                config,
                (benchCase, setup) =>
                {
                    string systemPrompt = trimExamples
                        ? LlmIntentService.TrimExamplesSection(setup.SystemPromptFull)
                        : setup.SystemPromptFull;
                    var sentence = new Sentence(benchCase.Text, benchCase.Words);
                    // Même sérialisation du message utilisateur que le contrôleur.
                    string userContent = JsonConvert.SerializeObject(new { sentence.Text, sentence.Words });

                    var stopwatch = Stopwatch.StartNew();
                    LlmIntentService.CallResult call = EditModeSync.RunSync(() =>
                        LlmIntentService.CallChatCompletionsAsync(
                            http, local ? url : null, apiKey, model, systemPrompt, userContent,
                            jsonObjectFormat: !local));
                    stopwatch.Stop();

                    return new CaseRun
                    {
                        ProducedJson = call.Content,
                        TotalMs      = stopwatch.Elapsed.TotalMilliseconds,
                        HttpMs       = call.HttpMs,
                        Error        = call.Error
                    };
                },
                conditions);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Boucle principale
        // ─────────────────────────────────────────────────────────────────────

        private void RunBenchmark(string config, Func<BenchCase, LocaleSetup, CaseRun> runner, string conditions)
        {
            List<BenchCase> cases = LoadCases();

            // fr d'abord, en ensuite : minimise les bascules de vocabulaires statiques par locale.
            List<BenchCase> ordered = cases.Where(c => c.Lang == "fr")
                                           .Concat(cases.Where(c => c.Lang != "fr")).ToList();

            var results = new List<CaseResult>();
            foreach (BenchCase benchCase in ordered)
            {
                LocaleSetup setup = GetLocaleSetup(benchCase.Lang);
                CaseRun run;
                try
                {
                    run = runner(benchCase, setup);
                }
                catch (Exception e)
                {
                    run = new CaseRun { Error = $"{e.GetType().Name}: {e.Message}" };
                }
                results.Add(Evaluate(benchCase, setup, run));
            }

            results = results.OrderBy(r => r.Case.Id, StringComparer.Ordinal).ToList();
            WriteReports(config, results, conditions);
            Assert.AreEqual(cases.Count, results.Count, "Tous les cas doivent avoir été évalués.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Évaluation d'un cas
        // ─────────────────────────────────────────────────────────────────────

        private CaseResult Evaluate(BenchCase benchCase, LocaleSetup setup, CaseRun run)
        {
            var result = new CaseResult { Case = benchCase, Run = run };

            // Substitution du placeholder comme dans le prompt système (nom du composant pointeur).
            string expectedJson = benchCase.ExpectedRawJson.Replace("{pointerTerm}", setup.PointerTerm);
            List<Command> expected = DeserializeCommands(expectedJson) ?? new List<Command>();
            List<Command> produced = null;

            if (run.Error != null)
            {
                result.PredictedOutcome = "error";
                result.Notes.Add($"erreur d'appel : {run.Error}");
            }
            else
            {
                produced = DeserializeCommands(run.ProducedJson);
                if (run.ProducedJson != null && produced == null)
                    result.Notes.Add("JSON produit non désérialisable");
                result.PredictedOutcome = PredictOutcome(produced);
            }

            // Exactitude du type : pour les cas de rejet (0 commande attendue), le succès est
            // l'absence de commande produite.
            if (expected.Count == 0)
                result.TypeOk = produced == null || produced.Count == 0 || produced.All(c => c is UnknownCommand);
            else
                result.TypeOk = produced != null && produced.Count == expected.Count &&
                                produced.Zip(expected, (p, e) => p.Type == e.Type).All(x => x);
            if (!result.TypeOk)
                result.Notes.Add($"type: attendu [{string.Join(",", expected.Select(e => e.Type))}] " +
                                 $"produit [{(produced == null ? "∅" : string.Join(",", produced.Select(p => p.Type)))}]");

            // F-mesure des paramètres : appariement par type puis contenu, agrégé sur le jeu.
            // Les paramètres sont notés indépendamment du type de commande (métrique orthogonale
            // à l'exactitude du type).
            (result.Tp, result.Fp, result.Fn)             = ScoreParameters(produced, expected, new Cmp());
            (result.TpNoTs, result.FpNoTs, result.FnNoTs) = ScoreParameters(produced, expected, new Cmp { NoTs = true });
            (result.TpRelax, result.FpRelax, result.FnRelax) =
                ScoreParameters(produced, expected, new Cmp { NoTs = true, UnorderedAnd = true });
            AddMismatchNotes(result, produced, expected);

            // Issue : « no_match » exige la scène — au niveau extraction, équivaut à « executed ».
            result.OutcomeOk = benchCase.ExpectedOutcome switch
            {
                "executed"       => result.PredictedOutcome == "executed",
                "no_match"       => result.PredictedOutcome == "executed",
                "clarification"  => result.PredictedOutcome == "clarification",
                "not_understood" => result.PredictedOutcome == "not_understood",
                _                => false
            };

            return result;
        }

        /// <summary>Miroir de MultimodalityController.DeserializeCommand (tolérance wrapper).</summary>
        private static List<Command> DeserializeCommands(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                List<Command> commands = null;
                string trimmed = json.TrimStart();
                if (trimmed.StartsWith("{"))
                {
                    JObject wrapper = JObject.Parse(json);
                    JToken array = wrapper.Properties().Select(p => p.Value)
                                          .FirstOrDefault(v => v.Type == JTokenType.Array);
                    if (array != null) commands = array.ToObject<List<Command>>();
                    else if (wrapper["type"] != null) commands = new List<Command> { wrapper.ToObject<Command>() };
                }
                commands ??= JsonConvert.DeserializeObject<List<Command>>(json);
                return commands?.Where(c => c != null).ToList();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Issue prédite au niveau extraction : rien → not_understood ; paramètre requis
        /// manquant (restrictions OWL, comme ResolveCommands) → clarification ; sinon executed.
        /// </summary>
        private static string PredictOutcome(List<Command> produced)
        {
            if (produced == null || produced.Count == 0 || produced.Any(c => c is UnknownCommand))
                return "not_understood";
            foreach (Command command in produced)
                if (ClarificationVocabulary.GetMissingParameterPrompt(command) != null)
                    return "clarification";
            return "executed";
        }

        // ─────────────────────────────────────────────────────────────────────
        // Comparaison structurelle (jamais textuelle : les clés peuvent être réordonnées)
        // ─────────────────────────────────────────────────────────────────────

        private struct Cmp
        {
            public bool NoTs;         // ignore les horodatages
            public bool NoLimit;      // ignore limit (diagnostic uniquement)
            public bool UnorderedAnd; // conjonctions pures (AND seul) comparées comme multi-ensembles
        }

        private static (int tp, int fp, int fn) ScoreParameters(List<Command> produced, List<Command> expected, Cmp options)
        {
            List<Parameter> expectedParams = expected.SelectMany(c => c.Parameters ?? new List<Parameter>()).ToList();
            List<Parameter> producedParams = (produced ?? new List<Command>())
                .SelectMany(c => c.Parameters ?? new List<Parameter>()).ToList();

            var remaining = new List<Parameter>(producedParams);
            int tp = 0;
            foreach (Parameter expectedParam in expectedParams)
            {
                Parameter match = remaining.FirstOrDefault(p => ParameterEquals(p, expectedParam, options));
                if (match != null)
                {
                    tp++;
                    remaining.Remove(match);
                }
            }
            return (tp, remaining.Count, expectedParams.Count - tp);
        }

        private static bool ParameterEquals(Parameter produced, Parameter expected, Cmp options)
        {
            if (produced.GetType() != expected.GetType()) return false;

            switch (expected)
            {
                case SelectionParameter expectedSelection:
                    var producedSelection = (SelectionParameter)produced;
                    if (!options.NoLimit && producedSelection.Limit != expectedSelection.Limit) return false;
                    if (!OrderEquals(producedSelection.Order, expectedSelection.Order)) return false;
                    return FiltersEqual(producedSelection.Filters, expectedSelection.Filters, options);

                case ColorParameter expectedColor:
                    // Le jeu annoté ne porte jamais d'horodatage sur ColorParameter : valeur seule.
                    return string.Equals(((ColorParameter)produced).Value, expectedColor.Value, StringComparison.Ordinal);

                case PointParameter expectedPoint:
                    var producedPoint = (PointParameter)produced;
                    if (!string.Equals(producedPoint.Value, expectedPoint.Value, StringComparison.Ordinal)) return false;
                    return options.NoTs || TimestampEquals(producedPoint.Timestamp, expectedPoint.Timestamp);

                default:
                    return produced.Type == expected.Type;
            }
        }

        private static bool FiltersEqual(List<FilterElement> produced, List<FilterElement> expected, Cmp options)
        {
            produced ??= new List<FilterElement>();
            expected ??= new List<FilterElement>();
            if (produced.Count != expected.Count) return false;

            // Conjonction pure des deux côtés + mode non ordonné : multi-ensemble de conditions
            // (une permutation de « A AND B » est sémantiquement identique). Dès qu'un OR est
            // présent, la position redevient significative — comparaison strictement positionnelle.
            bool pureAnd = produced.All(f => !f.IsOperator || f.IsAnd) &&
                           expected.All(f => !f.IsOperator || f.IsAnd);
            if (options.UnorderedAnd && pureAnd)
            {
                List<Condition> producedConditions = produced.Where(f => !f.IsOperator).Select(f => f.Condition).ToList();
                List<Condition> expectedConditions = expected.Where(f => !f.IsOperator).Select(f => f.Condition).ToList();
                if (producedConditions.Count != expectedConditions.Count) return false;
                var remaining = new List<Condition>(producedConditions);
                foreach (Condition expectedCondition in expectedConditions)
                {
                    Condition match = remaining.FirstOrDefault(c => ConditionEquals(c, expectedCondition, options));
                    if (match == null) return false;
                    remaining.Remove(match);
                }
                return true;
            }

            for (int i = 0; i < expected.Count; i++)
            {
                FilterElement p = produced[i], e = expected[i];
                if (p.IsOperator != e.IsOperator) return false;
                if (e.IsOperator)
                {
                    if (!string.Equals(p.Operator, e.Operator, StringComparison.OrdinalIgnoreCase)) return false;
                }
                else if (!ConditionEquals(p.Condition, e.Condition, options))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool ConditionEquals(Condition produced, Condition expected, Cmp options)
        {
            if (produced == null || expected == null) return produced == expected;
            if (!string.Equals(produced.Type, expected.Type, StringComparison.OrdinalIgnoreCase)) return false;
            bool valuesEqual = string.IsNullOrEmpty(expected.Value)
                ? string.IsNullOrEmpty(produced.Value)
                : string.Equals(produced.Value, expected.Value, StringComparison.Ordinal);
            if (!valuesEqual) return false;
            return options.NoTs || TimestampEquals(produced.Timestamp, expected.Timestamp);
        }

        private static bool TimestampEquals(DateTime produced, DateTime expected)
            => NormalizeUtc(produced) == NormalizeUtc(expected);

        // Kind=Unspecified (LLM sans fuseau) est interprété comme UTC — les horodatages du jeu
        // de cas sont tous suffixés « Z », un ToUniversalTime local fausserait la comparaison.
        private static DateTime NormalizeUtc(DateTime dateTime) => dateTime.Kind switch
        {
            DateTimeKind.Utc         => dateTime,
            DateTimeKind.Local       => dateTime.ToUniversalTime(),
            _                        => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };

        private static bool OrderEquals(Order produced, Order expected)
        {
            List<Criteria> producedCriterias = produced?.Criterias ?? new List<Criteria>();
            List<Criteria> expectedCriterias = expected?.Criterias ?? new List<Criteria>();
            if (producedCriterias.Count != expectedCriterias.Count) return false;
            for (int i = 0; i < expectedCriterias.Count; i++)
            {
                if (!string.Equals(producedCriterias[i].Type, expectedCriterias[i].Type, StringComparison.OrdinalIgnoreCase)) return false;
                if (producedCriterias[i].Desc != expectedCriterias[i].Desc) return false;
            }
            return true;
        }

        // Diagnostic lisible des paramètres non appariés (cause probable du premier écart).
        private void AddMismatchNotes(CaseResult result, List<Command> produced, List<Command> expected)
        {
            List<Parameter> expectedParams = expected.SelectMany(c => c.Parameters ?? new List<Parameter>()).ToList();
            List<Parameter> producedParams = (produced ?? new List<Command>())
                .SelectMany(c => c.Parameters ?? new List<Parameter>()).ToList();

            var remaining = new List<Parameter>(producedParams);
            foreach (Parameter expectedParam in expectedParams)
            {
                Parameter strictMatch = remaining.FirstOrDefault(p => ParameterEquals(p, expectedParam, new Cmp()));
                if (strictMatch != null) { remaining.Remove(strictMatch); continue; }

                Parameter candidate = remaining.FirstOrDefault(p => p.GetType() == expectedParam.GetType());
                if (candidate == null)
                {
                    result.Notes.Add($"{expectedParam.Type} manquant");
                    continue;
                }
                string reason =
                    ParameterEquals(candidate, expectedParam, new Cmp { NoTs = true })
                        ? "horodatages seuls"
                    : ParameterEquals(candidate, expectedParam, new Cmp { NoTs = true, NoLimit = true })
                        ? "limit (+ horodatages ?)"
                    : ParameterEquals(candidate, expectedParam, new Cmp { NoTs = true, UnorderedAnd = true })
                        ? "ordre des filtres (+ horodatages)"
                    : ParameterEquals(candidate, expectedParam, new Cmp { NoTs = true, UnorderedAnd = true, NoLimit = true })
                        ? "limit + ordre des filtres"
                        : "contenu des filtres/valeurs";
                result.Notes.Add($"{expectedParam.Type}: {reason}");
                remaining.Remove(candidate);
            }
            foreach (Parameter leftover in remaining)
                result.Notes.Add($"{leftover.Type} en trop");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Métriques agrégées + rapports
        // ─────────────────────────────────────────────────────────────────────

        private static double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            if (sorted.Count == 0) return 0;
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
        }

        private static (double p, double r, double f) Prf(int tp, int fp, int fn)
        {
            double p = tp + fp == 0 ? 1.0 : (double)tp / (tp + fp);
            double r = tp + fn == 0 ? 1.0 : (double)tp / (tp + fn);
            double f = p + r == 0 ? 0.0 : 2 * p * r / (p + r);
            return (p, r, f);
        }

        private static string Sanitize(string s) =>
            string.Concat(s.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_'));

        private static string ResultsDir
        {
            get
            {
                string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "BenchmarkResults");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        // Pourcentage en culture invariante : la culture du processus (éditeur/batch) peut
        // rendre « % » avec des variantes locales (ex: U+066A) illisibles dans le rapport.
        private static string Pct(double value, int decimals = 1)
            => (value * 100).ToString("F" + decimals, CultureInfo.InvariantCulture) + " %";

        private void WriteReports(string config, List<CaseResult> results, string conditions)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            int total = results.Count;

            // ── Agrégats globaux ─────────────────────────────────────────────
            int typeOk = results.Count(r => r.TypeOk);
            int outcomeOk = results.Count(r => r.OutcomeOk);
            int errors = results.Count(r => r.Run.Error != null);
            int tp = results.Sum(r => r.Tp), fp = results.Sum(r => r.Fp), fn = results.Sum(r => r.Fn);
            int tpNoTs = results.Sum(r => r.TpNoTs), fpNoTs = results.Sum(r => r.FpNoTs), fnNoTs = results.Sum(r => r.FnNoTs);
            int tpRelax = results.Sum(r => r.TpRelax), fpRelax = results.Sum(r => r.FpRelax), fnRelax = results.Sum(r => r.FnRelax);
            (double precision, double recall, double f1) = Prf(tp, fp, fn);
            (_, _, double f1NoTs) = Prf(tpNoTs, fpNoTs, fnNoTs);
            (_, _, double f1Relax) = Prf(tpRelax, fpRelax, fnRelax);

            int clarifLegit = results.Count(r => r.Case.ExpectedOutcome == "clarification" && r.PredictedOutcome == "clarification");
            int clarifWrong = results.Count(r => r.Case.ExpectedOutcome != "clarification" && r.PredictedOutcome == "clarification");
            int clarifMissed = results.Count(r => r.Case.ExpectedOutcome == "clarification" && r.PredictedOutcome != "clarification");

            double medianMs = Median(results.Where(r => r.Run.Error == null).Select(r => r.Run.TotalMs));
            List<double> httpTimes = results.Where(r => r.Run.HttpMs.HasValue).Select(r => r.Run.HttpMs.Value).ToList();
            double medianHttpMs = httpTimes.Count > 0 ? Median(httpTimes) : 0;

            // ── CSV par cas ─────────────────────────────────────────────────
            var caseLines = new List<string>
            {
                "id;lang;categorie;attendu;predit;outcome_ok;type_ok;tp;fp;fn;tp_sans_ts;fp_sans_ts;fn_sans_ts;latence_ms;http_ms;notes"
            };
            foreach (CaseResult r in results)
            {
                string notes = string.Join(" | ", r.Notes).Replace(";", ",").Replace("\n", " ");
                caseLines.Add(string.Join(";",
                    r.Case.Id, r.Case.Lang, r.Case.Category, r.Case.ExpectedOutcome, r.PredictedOutcome,
                    r.OutcomeOk ? 1 : 0, r.TypeOk ? 1 : 0,
                    r.Tp, r.Fp, r.Fn, r.TpNoTs, r.FpNoTs, r.FnNoTs,
                    r.Run.Error == null ? r.Run.TotalMs.ToString("F1", inv) : "",
                    r.Run.HttpMs?.ToString("F1", inv) ?? "",
                    $"\"{notes}\""));
            }
            string casesPath = Path.Combine(ResultsDir, $"intent_extraction_{config}_cases.csv");
            File.WriteAllLines(casesPath, caseLines);

            // ── Sorties brutes (traçabilité) ────────────────────────────────
            string outputsPath = Path.Combine(ResultsDir, $"intent_extraction_{config}_outputs.jsonl");
            File.WriteAllLines(outputsPath, results.Select(r => new JObject
            {
                ["id"] = r.Case.Id,
                ["produced"] = r.Run.ProducedJson,
                ["error"] = r.Run.Error
            }.ToString(Formatting.None)));

            // ── Rapport Markdown (global + par catégorie) ───────────────────
            var md = new List<string>
            {
                $"# Extraction d'intention — configuration « {config} »",
                "",
                $"- Date : {DateTime.Now:yyyy-MM-dd HH:mm}",
                $"- Conditions : {conditions}.",
                "- Conditions communes : movePointDelayMs = 0 ; context.history non injecté ; " +
                "« no_match » compté comme « executed » au niveau extraction ; comparaison structurelle " +
                "(type de commande, paramètres appariés par type puis contenu — filtres [type, valeur, " +
                "horodatage], opérateurs AND/OR positionnels, limit, order).",
                errors > 0 ? $"- ⚠ {errors} cas en erreur d'appel (comptés comme échecs partout — relancer si transitoire)." : null,
                "",
                "## Métriques globales",
                "",
                "| Métrique | Valeur |",
                "|---|---|",
                $"| Exactitude du type | {typeOk}/{total} = {Pct((double)typeOk / total)} |",
                $"| Paramètres — précision (stricte) | {Pct(precision)} ({tp} VP / {fp} FP / {fn} FN) |",
                $"| Paramètres — rappel (strict) | {Pct(recall)} |",
                $"| Paramètres — F-mesure (stricte) | {Pct(f1)} |",
                $"| Paramètres — F-mesure sans horodatages (diagnostic) | {Pct(f1NoTs)} |",
                $"| Paramètres — F-mesure sans horodatages, conjonctions non ordonnées (diagnostic) | {Pct(f1Relax)} |",
                $"| Clarifications légitimes | {clarifLegit}/{results.Count(r => r.Case.ExpectedOutcome == "clarification")} |",
                $"| Clarifications à tort | {clarifWrong} |",
                $"| Clarifications manquées | {clarifMissed} |",
                $"| Exactitude de l'issue | {outcomeOk}/{total} = {Pct((double)outcomeOk / total)} |",
                $"| Latence médiane | {medianMs.ToString("F1", inv)} ms |",
                httpTimes.Count > 0 ? $"| Latence HTTP médiane (réseau + inférence) | {medianHttpMs.ToString("F1", inv)} ms |" : null,
                "",
                "## Détail par catégorie",
                "",
                "| Catégorie | n | Type OK | F1 stricte | F1 sans horodatages | Issue OK | Latence médiane (ms) |",
                "|---|---|---|---|---|---|---|"
            };
            foreach (var group in results.GroupBy(r => r.Case.Category).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                int gTp = group.Sum(r => r.Tp), gFp = group.Sum(r => r.Fp), gFn = group.Sum(r => r.Fn);
                int gTpNoTs = group.Sum(r => r.TpNoTs), gFpNoTs = group.Sum(r => r.FpNoTs), gFnNoTs = group.Sum(r => r.FnNoTs);
                (_, _, double gF1) = Prf(gTp, gFp, gFn);
                (_, _, double gF1NoTs) = Prf(gTpNoTs, gFpNoTs, gFnNoTs);
                md.Add($"| {group.Key} | {group.Count()} | {group.Count(r => r.TypeOk)}/{group.Count()} " +
                       $"| {Pct(gF1, 0)} | {Pct(gF1NoTs, 0)} | {group.Count(r => r.OutcomeOk)}/{group.Count()} " +
                       $"| {Median(group.Where(r => r.Run.Error == null).Select(r => r.Run.TotalMs)).ToString("F1", inv)} |");
            }

            md.Add("");
            md.Add("## Causes d'écart les plus fréquentes");
            md.Add("");
            var causes = results.SelectMany(r => r.Notes)
                                .Where(n => !n.StartsWith("type:"))
                                .GroupBy(n => n).OrderByDescending(g => g.Count()).Take(12);
            foreach (var cause in causes)
                md.Add($"- {cause.Count()} × {cause.Key}");
            md.Add("");
            md.Add($"Détail par cas : `{Path.GetFileName(casesPath)}` ; sorties brutes : `{Path.GetFileName(outputsPath)}`.");

            string reportPath = Path.Combine(ResultsDir, $"intent_extraction_{config}_report.md");
            File.WriteAllLines(reportPath, md.Where(l => l != null));

            // ── Résumé cumulatif : une ligne par configuration ───────────────
            string summaryPath = Path.Combine(ResultsDir, "intent_extraction_summary.csv");
            const string header = "config;date;exactitude_type;param_precision;param_rappel;param_f1;" +
                                  "param_f1_sans_ts;param_f1_sans_ts_non_ordonne;clarif_legitimes;clarif_a_tort;" +
                                  "clarif_manquees;exactitude_issue;latence_mediane_ms;latence_http_mediane_ms;erreurs;cas";
            var summaryLines = File.Exists(summaryPath)
                ? File.ReadAllLines(summaryPath).Where(l => l.Length > 0 && !l.StartsWith(config + ";") && l != header).ToList()
                : new List<string>();
            summaryLines.Insert(0, header);
            summaryLines.Add(string.Join(";",
                config, DateTime.Now.ToString("yyyy-MM-dd HH:mm", inv),
                ((double)typeOk / total).ToString("F3", inv),
                precision.ToString("F3", inv), recall.ToString("F3", inv), f1.ToString("F3", inv),
                f1NoTs.ToString("F3", inv), f1Relax.ToString("F3", inv),
                clarifLegit, clarifWrong, clarifMissed,
                ((double)outcomeOk / total).ToString("F3", inv),
                medianMs.ToString("F1", inv),
                httpTimes.Count > 0 ? medianHttpMs.ToString("F1", inv) : "",
                errors, total));
            File.WriteAllLines(summaryPath, summaryLines);

            // ── Résumé console ───────────────────────────────────────────────
            Debug.Log(
                $"[Benchmark:{config}] type {typeOk}/{total} ({Pct((double)typeOk / total)}) | " +
                $"paramètres P={Pct(precision)} R={Pct(recall)} F1={Pct(f1)} (sans ts : {Pct(f1NoTs)} ; " +
                $"sans ts, non ordonné : {Pct(f1Relax)}) | clarifications légitimes {clarifLegit}, à tort {clarifWrong}, " +
                $"manquées {clarifMissed} | issue {outcomeOk}/{total} | latence médiane {medianMs:F1} ms" +
                (httpTimes.Count > 0 ? $" (HTTP {medianHttpMs:F1} ms)" : "") +
                (errors > 0 ? $" | ⚠ {errors} erreurs d'appel" : "") +
                $"\nRapports : {reportPath}");
        }
    }
}
