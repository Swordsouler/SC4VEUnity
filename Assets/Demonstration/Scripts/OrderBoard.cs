using Sc4ve.Multimodality;
using Sc4ve.Multimodality.Intent;
using Sc4ve.Voice;
using Sven.Content;
using Sven.Multimodality;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
// Sc4ve.Multimodality.Intent a sa propre classe Color (l'annotation de couleur).
using Color = UnityEngine.Color;

namespace Sc4ve.Demonstration
{
    /// <summary>
    /// La tablette de la main gauche (§5 du README) : ce que le système a fait de la dernière
    /// phrase, et où en est chaque table. Trois bandes, dessinées au lancement d'après la
    /// maquette validée (22 × 17 cm) :
    ///
    ///  - la VOIX : la phrase entendue, en entier — jusqu'à trois lignes, puis le texte
    ///    rétrécit, il n'est jamais coupé —, son trajet étape par étape (IntentPipeline :
    ///    règles, LLM, recherche sémantique, exécution ; une étape jamais sollicitée ne
    ///    s'affiche pas), et le résultat ;
    ///  - la SALLE : les quatre tables en plan, placées comme le joueur les voit depuis la
    ///    passe — le fond en haut, la gauche à gauche. Les tables ne sont pas numérotées
    ///    (§5) : c'est leur place qui les désigne. Chacune dit où en est sa commande —
    ///    commande, cuisine, service, débarrassage —, en jaune quand c'est au joueur de
    ///    parler ; après DEUX phrases non comprises de suite, la phrase à dire s'y écrit ;
    ///  - le PIED : ce que font le cuisinier et le serveur, et le score.
    ///
    /// **Une projection, jamais une seconde source de vérité.** Elle ne détient aucun état :
    /// clients, cuisinier, serveur et assiettes sont relus à chaque rafraîchissement, le
    /// trajet de la phrase dans IntentPipeline. Seule exception : l'état du micro et la
    /// dernière phrase transcrite n'existent que comme ÉVÉNEMENTS (VoiceProcessor,
    /// BaseSpeechToText) — il n'y a rien à relire, la tablette les mémorise donc, et c'est
    /// tout ce qu'elle mémorise. Non interactive, non sémantisée : aucun collider, aucun
    /// SemantizationCore — elle n'intercepte pas le rayon et n'entre pas dans le graphe.
    ///
    /// Primitives et TextMesh, comme les panneaux de XRPanel : rien d'autre dans la démo
    /// n'utilise uGUI.
    /// </summary>
    public class OrderBoard : MonoBehaviour
    {
        // ── La géométrie de la maquette, dessinée à 40 px par cm ──────────────
        private const float Px = 0.00025f;          // 1 px de la maquette = 0,25 mm
        private const float WidthPx = 880f;         // 22 cm
        private const float HeightPx = 680f;        // 17 cm
        private const float Thickness = 0.006f;
        private const int FontSize = 64;            // résolution des glyphes, pas leur taille

        private const float Left = 20f;
        private const float Right = 860f;
        private const float TranscriptTop = 16f;
        private const float TranscriptHeight = 84f; // trois lignes
        private const float TranscriptWidth = 640f;
        private const float LineHeight = 28f;
        private const float ChipsTop = 106f;
        private const float ChipHeight = 50f;
        private const float OutcomeTop = 162f;
        private const float VoiceRule = 200f;
        private const float GridTop = 212f;
        private const float CardWidth = 415f;
        private const float CardHeight = 180f;
        private const float Gap = 10f;
        private const float SlotWidth = CardWidth - 42f;
        private const float FooterRule = 592f;

        // Vers le joueur, qui lit depuis -z — comme un TextMesh et la face d'un quad.
        private const float BorderZ = -0.0002f;
        private const float FillZ = -0.0004f;
        private const float TrackZ = -0.0006f;
        private const float MarkZ = -0.0008f;
        private const float TextZ = -0.0011f;

        // 4 Hz en temps RÉEL : c'est de l'affichage, il ne doit pas se figer au ralenti.
        private const float RefreshInterval = 0.25f;

        /// <summary>Un énoncé sans audio capté ne produit AUCUN résultat : au-delà de ce
        /// délai, « Transcription… » redevient « prêt » au lieu de tourner pour toujours.</summary>
        private const float TranscriptionTimeout = 10f;

        /// <summary>Secondes réelles pendant lesquelles une table montre « départ sans service ».</summary>
        private const float DepartureShown = 6f;

        /// <summary>Sous ce reste de patience, la carte se cerne de rouge.</summary>
        private const float UrgentRatio = 0.25f;

        private static readonly Color Ground = Hex("29332B");
        private static readonly Color Panel = Hex("323D35");
        private static readonly Color PanelQuiet = Hex("2D3830");
        private static readonly Color Edge = Hex("46544A");
        private static readonly Color EdgeQuiet = Hex("3A463D");
        private static readonly Color Track = Hex("1F2621");
        private static readonly Color Cream = Hex("EBE6D1");
        private static readonly Color Dim = Hex("A9B3AA");
        private static readonly Color Yellow = Hex("F2C14E");
        private static readonly Color YellowPanel = Hex("3B3A2C");
        private static readonly Color Green = Hex("59A64D");
        private static readonly Color GreenText = Hex("7CC46E");
        private static readonly Color Red = Hex("BF382E");
        private static readonly Color RedText = Hex("F08A7E");
        private static readonly Color Todo = Hex("46544A");
        private static readonly Color DoneQuiet = Hex("3F6B38");
        private static readonly Color TodoQuiet = Hex("3A463D");
        private static readonly Color Grey = Hex("BBBBBB");
        private static readonly Color Listening = Hex("FF6666");
        private static readonly Color Transcribing = Hex("FFCC66");

        private enum MicState { Idle, Listening, Transcribing }

        private sealed class Chip
        {
            public Renderer Border, Fill;
            public TextMesh Name, Status;
        }

        private sealed class CardView
        {
            public float X, Y;
            public Renderer Border, Fill, Track, Bar;
            public readonly Renderer[] Segments = new Renderer[4];
            public TextMesh Title, Right, Dish, Slot;
        }

        /// <summary>Ce qu'une carte de table affiche, relu à chaque rafraîchissement.</summary>
        private struct CardModel
        {
            public string Title;
            public Color TitleColor;
            public string Right;
            public Color RightColor;
            public bool RightBold;
            public float Bar;           // < 0 : pas de barre
            public Color BarColor;
            public string Dish;
            public Color DishColor;
            public FontStyle DishStyle;
            public Color[] Segments;    // null : pas d'étapes
            public string Slot;
            public Color Border;
            public Color Fill;
            public bool Thick;
        }

        /// <summary>Un morceau de la case d'état d'une carte : préfixe en gras, texte, couleur.</summary>
        private readonly struct Piece
        {
            public readonly string Bold;
            public readonly string Text;
            public readonly Color Color;

            public Piece(string bold, string text, Color color)
            {
                Bold = bold;
                Text = text;
                Color = color;
            }
        }

        private Font _font;
        private bool _built;
        private float _nextRefresh;
        private ServiceProgression _progression;

        private VoiceProcessor _voice;
        private BaseSpeechToText _recognizer;
        private MicState _mic;
        private float _transcribingSince;
        private string _lastHeard;

        private TextMesh _micDot, _transcript, _departLabel, _departPips, _outcome;
        private string _transcriptShown;
        private readonly Chip[] _chips = new Chip[4];
        private readonly TextMesh[] _arrows = new TextMesh[3];
        private readonly CardView[] _cards = new CardView[4];
        private TextMesh _cook, _waiter, _served, _refusals;
        private readonly List<SemantizationCore> _tables = new();

        // Vocabulaire, pas état du monde : les libellés des recettes, lus une fois.
        private readonly Dictionary<string, string> _recipeLabels = new();
        private readonly HashSet<string> _labelsAsked = new();
        private string _labelsLocale;

        // ─────────────────────────────────────────────────────────────────────
        // Cycle de vie
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Une tablette posée par une version précédente du builder porte son ancien
            // TextMesh : on ne dessine pas par-dessus, on demande une reconstruction.
            if (GetComponent<TextMesh>() != null)
            {
                Debug.LogError("[Tablette] Tablette d'une version précédente : relancer « SC4VE > Démonstration > 1 ».");
                enabled = false;
                return;
            }

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
            _built = true;
        }

        private void Start()
        {
            _progression = FindAnyObjectByType<ServiceProgression>();

            _voice = FindAnyObjectByType<VoiceProcessor>();
            if (_voice != null)
            {
                // Les MÊMES bornes que le ralenti du temps (ListeningTimeScale) : l'indicateur
                // s'allume exactement quand la prise de parole compte — appui et relâchement
                // du push-to-talk, ou fenêtre de voix en mode VAD.
                _voice.OnSpeechStart += OnListeningStarted;
                _voice.OnRecordingStop += OnListeningStopped;
            }

            _recognizer = FindAnyObjectByType<BaseSpeechToText>();
            if (_recognizer != null) _recognizer.OnTranscriptionResult += OnHeard;
        }

        private void OnDestroy()
        {
            if (_voice != null)
            {
                _voice.OnSpeechStart -= OnListeningStarted;
                _voice.OnRecordingStop -= OnListeningStopped;
            }
            if (_recognizer != null) _recognizer.OnTranscriptionResult -= OnHeard;
        }

        private void OnListeningStarted() => _mic = MicState.Listening;

        private void OnListeningStopped()
        {
            if (_mic != MicState.Listening) return;
            _mic = MicState.Transcribing;
            _transcribingSince = Time.unscaledTime;
        }

        private void OnHeard(string text)
        {
            string sentence = ExtractSentence(text);
            if (!string.IsNullOrWhiteSpace(sentence)) _lastHeard = sentence;
            _mic = MicState.Idle;
        }

        /// <summary>
        /// Les moteurs n'émettent pas la phrase nue mais leur JSON — Whisper : {"text": …,
        /// "result": [horodatages mot à mot]} ; Vosk : ses alternatives. La tablette ne veut
        /// que la PHRASE : le champ « text » s'il existe, la première alternative sinon, et
        /// le brut en dernier recours.
        /// </summary>
        private static string ExtractSentence(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            raw = raw.Trim();
            if (!raw.StartsWith("{")) return raw;

            try
            {
                var parsed = Newtonsoft.Json.Linq.JObject.Parse(raw);
                string text = (string)parsed["text"]
                              ?? (string)parsed.SelectToken("alternatives[0].text");
                return string.IsNullOrWhiteSpace(text) ? raw : text.Trim();
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return raw;
            }
        }

        private void Update()
        {
            if (!_built || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;

            bool french = UserData.Locale == "fr";
            if (_labelsLocale != UserData.Locale)
            {
                _labelsLocale = UserData.Locale;
                _recipeLabels.Clear();
                _labelsAsked.Clear();
            }

            RefreshVoice(french);
            RefreshRoom(french);
        }

        // ─────────────────────────────────────────────────────────────────────
        // La voix : la phrase, son trajet, le résultat
        // ─────────────────────────────────────────────────────────────────────

        private void RefreshVoice(bool french)
        {
            if (_mic == MicState.Transcribing && Time.unscaledTime - _transcribingSince > TranscriptionTimeout)
                _mic = MicState.Idle;

            switch (_mic)
            {
                case MicState.Listening:
                    Tint(_micDot, Listening);
                    ShowTranscript(french ? "J'écoute…" : "Listening…", Listening, FontStyle.Bold);
                    break;
                case MicState.Transcribing:
                    Tint(_micDot, Transcribing);
                    ShowTranscript(french ? "Transcription…" : "Transcribing…", Transcribing, FontStyle.Normal);
                    break;
                default:
                    Tint(_micDot, Grey);
                    ShowTranscript(string.IsNullOrEmpty(_lastHeard)
                            ? (french ? "micro prêt" : "mic ready")
                            : (french ? "Entendu : " : "Heard: ") + Quote(_lastHeard, french),
                        Grey, FontStyle.Normal);
                    break;
            }

            // Pendant qu'on parle, le trajet de la phrase précédente n'a plus rien à dire.
            bool idle = _mic == MicState.Idle;
            ShowChips(idle ? IntentPipeline.Steps : null, french);
            ShowOutcome(idle ? OutcomeLine(french) : ("", Cream, FontStyle.Normal));
        }

        /// <summary>
        /// La phrase en entier : jusqu'à trois lignes à la taille normale ; au-delà, le texte
        /// RÉTRÉCIT plutôt que d'être coupé. Découpée seulement quand elle change.
        /// </summary>
        private void ShowTranscript(string phrase, Color color, FontStyle style)
        {
            Tint(_transcript, color);
            string key = phrase + "\u0001" + style;
            if (key == _transcriptShown) return;
            _transcriptShown = key;

            float size = 24f;
            List<string> lines = new();
            for (int step = 10; step >= 6; step--)
            {
                size = 24f * step / 10f;
                lines = Wrap(phrase, TranscriptWidth, size, style);
                if (lines.Count * LineHeight * step / 10f <= TranscriptHeight + 0.5f) break;
            }

            _transcript.fontStyle = style;
            _transcript.characterSize = Character(size);
            Write(_transcript, string.Join("\n", lines));
        }

        /// <summary>Les étapes SOLLICITÉES, et elles seules, de gauche à droite.</summary>
        private void ShowChips(IReadOnlyList<IntentPipeline.Step> steps, bool french)
        {
            int count = steps?.Count ?? 0;
            float x = Left;
            for (int i = 0; i < _chips.Length; i++)
            {
                Chip chip = _chips[i];
                bool on = i < count;
                Show(chip, on);
                if (i > 0) _arrows[i - 1].gameObject.SetActive(on);
                if (!on) continue;

                if (i > 0)
                {
                    Place(_arrows[i - 1], x + 8f, ChipsTop + 13f);
                    x += 16f + Measure("→", 20f);
                }

                IntentPipeline.Step step = steps[i];
                string name = StageName(step.Stage, french);
                (string status, Color statusColor, Color border, Color fill) = Look(step, french);
                float width = 32f + Mathf.Max(Measure(name, 20f, FontStyle.Bold), Measure(status, 18f));

                Frame(chip.Border, x, ChipsTop, width, ChipHeight, border);
                Frame(chip.Fill, x + 2f, ChipsTop + 2f, width - 4f, ChipHeight - 4f, fill);
                Place(chip.Name, x + 14f, ChipsTop + 5f);
                Write(chip.Name, name);
                Place(chip.Status, x + 14f, ChipsTop + 27f);
                Write(chip.Status, status);
                Tint(chip.Status, statusColor);
                x += width;
            }
        }

        private static void Show(Chip chip, bool on)
        {
            chip.Border.gameObject.SetActive(on);
            chip.Fill.gameObject.SetActive(on);
            chip.Name.gameObject.SetActive(on);
            chip.Status.gameObject.SetActive(on);
        }

        private static string StageName(IntentPipeline.Stage stage, bool french) => stage switch
        {
            IntentPipeline.Stage.Rules => french ? "Règles" : "Rules",
            IntentPipeline.Stage.Llm => "LLM",
            IntentPipeline.Stage.Semantic => french ? "Recherche sémantique" : "Semantic search",
            _ => french ? "Exécution" : "Execution",
        };

        /// <summary>L'état d'une étape : son texte, sa couleur, son cadre et son fond.</summary>
        private static (string, Color, Color, Color) Look(IntentPipeline.Step step, bool french)
        {
            switch (step.Status)
            {
                case IntentPipeline.Status.Running:
                    return ($"… {OneDecimal(step.Seconds, french)} s", Yellow, Yellow, YellowPanel);
                case IntentPipeline.Status.Done:
                    string done = step.Reason == IntentPipeline.Reason.Kept ? Why(step.Reason, french)
                        : step.Stage == IntentPipeline.Stage.Execution ? (french ? "fait" : "done")
                        : Duration(step.Seconds, french);
                    return ($"✓ {done}", GreenText, Green, Panel);
                case IntentPipeline.Status.Relayed:
                    return ($"≈ {Why(step.Reason, french)}", Yellow, Yellow, YellowPanel);
                case IntentPipeline.Status.Question:
                    return ($"? {(step.Reason == IntentPipeline.Reason.WhichOne ? Why(step.Reason, french) : "question")}",
                            Yellow, Yellow, YellowPanel);
                case IntentPipeline.Status.Refused:
                    return ($"× {(french ? "refusé" : "refused")}", RedText, Red, Panel);
                default:
                    string why = step.Reason == IntentPipeline.Reason.None ? (french ? "échec" : "failed") : Why(step.Reason, french);
                    return ($"× {why}", RedText, Red, Panel);
            }
        }

        private static string Why(IntentPipeline.Reason reason, bool french) => reason switch
        {
            IntentPipeline.Reason.NothingRecognized => french ? "rien reconnu" : "nothing recognized",
            IntentPipeline.Reason.GuessedVerb => french ? "verbe deviné" : "verb guessed",
            IntentPipeline.Reason.MissedTarget => french ? "cible manquée" : "target missed",
            IntentPipeline.Reason.Kept => french ? "retenues" : "kept",
            IntentPipeline.Reason.NotUnderstood => french ? "rien compris" : "not understood",
            IntentPipeline.Reason.Timeout => french ? "délai dépassé" : "timed out",
            IntentPipeline.Reason.NoObject => french ? "aucun objet" : "no object",
            IntentPipeline.Reason.WhichOne => french ? "laquelle" : "which one",
            _ => "",
        };

        private static string Duration(float seconds, bool french)
            => seconds < 0.1f ? $"{Mathf.RoundToInt(seconds * 1000f)} ms" : $"{OneDecimal(seconds, french)} s";

        private static string OneDecimal(float value, bool french)
        {
            string text = value.ToString("0.0", CultureInfo.InvariantCulture);
            return french ? text.Replace('.', ',') : text;
        }

        /// <summary>La ligne de résultat, lue dans IntentPipeline.</summary>
        private (string, Color, FontStyle) OutcomeLine(bool french)
        {
            string outcome = IntentPipeline.Outcome;
            string spoken = IntentPipeline.Spoken;

            if (outcome == null)
            {
                if (IntentPipeline.Steps.Count == 0) return ("", Cream, FontStyle.Normal);
                bool llm = IntentPipeline.Steps.Any(s => s.Stage == IntentPipeline.Stage.Llm &&
                                                         s.Status == IntentPipeline.Status.Running);
                return llm
                    ? (french ? "Le LLM relit la phrase…" : "The LLM is reading the sentence…", Yellow, FontStyle.Normal)
                    : (french ? "Analyse…" : "Analysing…", Dim, FontStyle.Normal);
            }

            switch (outcome)
            {
                case "executed":
                    if (spoken == null) return (Understood(IntentPipeline.Command, french), Cream, FontStyle.Normal);
                    bool refused = IntentPipeline.Steps.Any(s => s.Status == IntentPipeline.Status.Refused);
                    return (Quote(spoken, french), refused ? RedText : Cream, FontStyle.Normal);
                case "clarification":
                case "disambiguation":
                    return ((french ? "Question : " : "Question: ") + Quote(spoken, french), Yellow, FontStyle.Normal);
                case "no_match":
                    return (Quote(spoken, french), RedText, FontStyle.Normal);
                case "not_understood":
                case "orphan":
                    if (IntentPipeline.HelpWanted)
                        return (french ? "Pas compris deux fois de suite — essayez une phrase en jaune"
                                       : "Not understood twice in a row — try a sentence in yellow",
                                Yellow, FontStyle.Bold);
                    return outcome == "orphan" && spoken != null
                        ? ((french ? "Question : " : "Question: ") + Quote(spoken, french), Yellow, FontStyle.Normal)
                        : (french ? "Pas compris — reformulez" : "Not understood — rephrase", RedText, FontStyle.Normal);
                default:
                    return (french ? "Erreur pendant le traitement" : "Processing error", RedText, FontStyle.Normal);
            }
        }

        /// <summary>« Compris : Prendre la commande — Alice » : la commande et ce qu'elle vise.</summary>
        private string Understood(Command command, bool french)
        {
            if (command == null) return "";

            var targets = new List<string>();
            foreach (Parameter parameter in command.Parameters ?? new List<Parameter>())
            {
                if (parameter is SelectionParameter selection && selection.Objects != null)
                    targets.AddRange(selection.Objects
                        .Where(o => o != null && o.GetComponent<CustomerOrder>() != null)
                        .Select(o => o.name));
                else if (parameter is RecipeParameter recipe && !string.IsNullOrEmpty(recipe.Value))
                    targets.Add(RecipeLabel(recipe.Value));
            }

            string tail = targets.Count > 0 ? " — " + string.Join(", ", targets.Distinct()) : "";
            return $"{Colored(french ? "Compris :" : "Understood:", Dim)} <b>{CommandLabel(command.Type, french)}</b>{tail}";
        }

        private static string CommandLabel(string type, bool french) => type switch
        {
            "TakeOrderCommand" => french ? "Prendre la commande" : "Take the order",
            "PrepareCommand" => french ? "Préparer" : "Prepare",
            "ServeCommand" => french ? "Servir" : "Serve",
            "ClearTableCommand" => french ? "Débarrasser" : "Clear the table",
            "RepeatOrderCommand" => french ? "Répéter la commande" : "Repeat the order",
            "CheckCommand" => french ? "Vérifier" : "Check",
            "GoToCommand" => french ? "Aller" : "Go",
            "StopCommand" => french ? "Arrêter" : "Stop",
            "PutInCommand" => french ? "Mettre dans" : "Put in",
            _ => string.IsNullOrEmpty(type) ? "—" : type.Replace("Command", ""),
        };

        /// <summary>Une seule ligne : trop longue, elle rétrécit — jamais de points de suspension.</summary>
        private void ShowOutcome((string text, Color color, FontStyle style) line)
        {
            float size = 22f;
            float width = Measure(Regex.Replace(line.text ?? "", "<[^>]+>", ""), size, line.style);
            if (width > Right - Left) size = Mathf.Max(size * 0.7f, size * (Right - Left) / width);

            _outcome.characterSize = Character(size);
            _outcome.fontStyle = line.style;
            Tint(_outcome, line.color);
            Write(_outcome, line.text);
        }

        // ─────────────────────────────────────────────────────────────────────
        // La salle : une carte par table
        // ─────────────────────────────────────────────────────────────────────

        private void RefreshRoom(bool french)
        {
            if (_tables.Count == 0) FindTables();

            // TOUS, inactifs compris : un client parti emporte son corps mais pas son
            // histoire — Served/Gone, refus et départ restent figés sur lui. Les modèles
            // jamais activés (ServiceProgression) restent Seated, sans départ : rien ne compte.
            CustomerOrder[] everyone = FindObjectsByType<CustomerOrder>(FindObjectsInactive.Include)
                .Where(c => c.Table != null)
                .ToArray();
            foreach (CustomerOrder customer in everyone)
                if (customer.WantedRecipe != null && customer.DishLabel != null)
                    _recipeLabels[customer.WantedRecipe] = customer.DishLabel;

            // Qualifié : Sc4ve.Demonstration.Cook est l'annotation du même nom.
            Sc4ve.Multimodality.Cook cook = Sc4ve.Multimodality.Cook.Find();
            List<string> queue = cook != null ? cook.PendingOrders.ToList() : new List<string>();
            Delegation waiter = FindAnyObjectByType<Delegation>();
            int gone = everyone.Count(c => c.State == CustomerOrder.Stage.Gone);
            int max = _progression != null ? _progression.MaxDepartures : 0;
            bool help = IntentPipeline.HelpWanted;

            for (int i = 0; i < _cards.Length; i++)
            {
                SemantizationCore table = i < _tables.Count ? _tables[i] : null;
                ShowCard(_cards[i], Describe(table, everyone, cook, queue, waiter, gone, max, help, french));
            }

            ShowDepartures(gone, max, french);
            ShowFooter(everyone, cook, queue, waiter, french);
        }

        /// <summary>
        /// Les tables, en plan : la salle vue depuis la passe — le fond en haut, la gauche à
        /// gauche. Rangées par z (arrondi au décimètre : une rangée partage son z), puis x.
        /// </summary>
        private void FindTables()
        {
            _tables.AddRange(FindObjectsByType<SemanticAnnotator>(FindObjectsInactive.Exclude)
                .Where(a => a.Annotations.Contains("sven:Table"))
                .Select(a => a.GetComponent<SemantizationCore>())
                .Where(t => t != null)
                .OrderByDescending(t => Mathf.Round(t.transform.position.z * 10f))
                .ThenBy(t => t.transform.position.x)
                .Take(_cards.Length));
        }

        /// <summary>
        /// Où en est la commande de cette table : commande, cuisine, service, débarrassage.
        /// Jaune quand c'est au joueur de parler, crème quand un serveur ou le cuisinier s'en
        /// occupe, vert quand c'est fait, bleu pendant le repas. Le plat n'apparaît qu'une
        /// fois la commande prise (critère 1 du lot 4) ; la contrainte, qu'après un refus.
        /// </summary>
        private CardModel Describe(SemantizationCore table, CustomerOrder[] everyone, Sc4ve.Multimodality.Cook cook, List<string> queue,
                                   Delegation waiter, int gone, int max, bool help, bool french)
        {
            var card = new CardModel
            {
                TitleColor = Cream, RightColor = Dim, Bar = -1f, DishColor = Cream, DishStyle = FontStyle.Normal,
                Border = Edge, Fill = Panel, Slot = "",
            };

            bool Heading(Delegation.Errand errand)
                => waiter != null && table != null && waiter.CurrentErrand == errand && waiter.Target == table;

            CustomerOrder customer = table == null ? null
                : everyone.FirstOrDefault(c => c.Table == table && c.gameObject.activeInHierarchy);

            if (customer != null)
            {
                string name = customer.name;
                card.Title = name;

                if (customer.State == CustomerOrder.Stage.Served)
                {
                    card.Bar = customer.MealRatio;
                    card.BarColor = CustomerOrder.MealColor;
                    card.Right = $"{Mathf.CeilToInt(customer.MealLeft)} s";
                    card.Dish = customer.DishLabel ?? "";
                    card.Segments = new[] { Green, Green, Green, CustomerOrder.MealColor };
                    card.Slot = Lines(Step(french ? "Repas" : "Meal", french ? $"{name} mange" : $"{name} is eating"));
                    return card;
                }

                card.Bar = customer.PatienceRatio;
                card.BarColor = Color.Lerp(CustomerOrder.ImpatientColor, CustomerOrder.PatientColor, customer.PatienceRatio);
                card.Right = $"{Mathf.CeilToInt(customer.PatienceLeft)} s";
                if (customer.PatienceRatio < UrgentRatio)
                {
                    card.RightColor = RedText;
                    card.RightBold = true;
                    card.Border = Red;
                    card.Thick = true;
                }

                if (customer.State == CustomerOrder.Stage.Seated)
                {
                    card.Dish = french ? "Commande à prendre" : "Order to take";
                    card.DishColor = Dim;
                    card.DishStyle = FontStyle.Italic;
                    bool going = Heading(Delegation.Errand.TakeOrder);
                    card.Segments = new[] { going ? Cream : Yellow, Todo, Todo, Todo };
                    card.Slot = going
                        ? Lines(Step(french ? "Commande" : "Order", french ? "le serveur y va" : "the waiter is on the way"))
                        : help
                            ? Lines(Hint(french ? $"Va voir {name}" : $"Take the order of {name}", french))
                            : Lines(Step(french ? "Commande" : "Order", french ? "attend un serveur" : "waiting for a waiter"));
                    return card;
                }

                string recipe = customer.WantedRecipe;
                string dish = customer.DishLabel ?? customer.FamilyLabel ?? (french ? "(illisible)" : "(unreadable)");
                card.Dish = dish;

                if (Heading(Delegation.Errand.Serve))
                {
                    card.Segments = new[] { Green, Green, Cream, Todo };
                    card.Slot = Lines(Step("Service", french ? "le serveur apporte" : "the waiter is bringing it"));
                }
                else if (Delegation.IsDishReady(recipe))
                {
                    card.Segments = new[] { Green, Green, Yellow, Todo };
                    card.Slot = help
                        ? Lines(Hint(french ? $"Sers {name}" : $"Serve {name}", french))
                        : Lines(Step("Service", french ? "prêt sur la passe" : "ready at the pass"));
                }
                else if (cook != null && cook.IsBusy && recipe != null && cook.TaskLabel == recipe)
                {
                    card.Segments = new[] { Green, Cream, Todo, Todo };
                    card.Slot = Lines(Step(french ? "Cuisine" : "Kitchen", french ? "en préparation" : "being prepared"));
                }
                else if (recipe != null && queue.Contains(recipe))
                {
                    int ahead = queue.IndexOf(recipe) + (cook != null && cook.IsBusy ? 1 : 0);
                    card.Segments = new[] { Green, Cream, Todo, Todo };
                    card.Slot = Lines(Step(french ? "Cuisine" : "Kitchen", french
                        ? $"noté, {ahead} plat{(ahead > 1 ? "s" : "")} avant"
                        : $"noted, {ahead} dish{(ahead > 1 ? "es" : "")} ahead"));
                }
                else
                {
                    card.Segments = new[] { Green, Yellow, Todo, Todo };
                    if (help)
                        card.Slot = Lines(Hint(PrepareHint(dish, french), french),
                                          Note(french ? "en regardant le cuisinier" : "while looking at the cook"));
                    else if (customer.LastRefusal != null)
                        card.Slot = Lines(Plain(Quote(customer.LastRefusal, french), RedText),
                                          Step(french ? "Cuisine" : "Kitchen", french ? "à refaire" : "to redo"));
                    else
                        card.Slot = Lines(Step(french ? "Cuisine" : "Kitchen", french ? "à demander au cuisinier" : "ask the cook"));
                }
                return card;
            }

            // Personne à table : quelqu'un vient-il d'en partir ?
            CustomerOrder last = table == null ? null : everyone
                .Where(c => c.Table == table && !c.gameObject.activeInHierarchy && c.DepartedAt >= 0f)
                .OrderByDescending(c => c.DepartedAt)
                .FirstOrDefault();

            if (last != null && last.State == CustomerOrder.Stage.Gone &&
                Time.unscaledTime - last.DepartedAt < DepartureShown)
            {
                card.Title = last.name;
                card.Right = french ? "× départ" : "× left";
                card.RightColor = RedText;
                card.RightBold = true;
                card.Dish = last.HasOrdered ? last.DishLabel ?? "" : (french ? "Commande non prise" : "Order not taken");
                card.DishColor = Dim;
                card.DishStyle = last.HasOrdered ? FontStyle.Normal : FontStyle.Italic;
                card.Segments = new[] { last.HasOrdered ? DoneQuiet : TodoQuiet, TodoQuiet, TodoQuiet, TodoQuiet };
                card.Border = Red;
                card.Thick = true;
                card.Slot = Lines(Step(french ? "Départ" : "Left", french ? "sans service" : "without being served", RedText),
                                  Plain(max > 0
                                      ? (french ? $"{gone} départ{(gone > 1 ? "s" : "")} sur {max}" : $"{gone} of {max} departures")
                                      : "", Dim));
                return card;
            }

            if (table != null && Delegation.HasDishOn(table))
            {
                card.Title = french ? "Assiette sale" : "Dirty plate";
                card.Dish = last != null && last.State == CustomerOrder.Stage.Served
                    ? (french ? $"{last.name} a fini" : $"{last.name} has finished")
                    : (french ? "Client parti" : "Customer gone");
                card.DishColor = Dim;
                card.DishStyle = FontStyle.Italic;
                bool clearing = Heading(Delegation.Errand.Clear);
                card.Segments = new[] { Green, Green, Green, clearing ? Cream : Yellow };
                card.Slot = clearing
                    ? Lines(Step(french ? "Débarrassage" : "Clearing", french ? "le serveur débarrasse" : "the waiter is clearing"))
                    : help
                        ? Lines(Hint(french ? "Débarrasse cette table" : "Clear this table", french),
                                Note(french ? "en la pointant" : "while pointing at it"))
                        : Lines(Step(french ? "Débarrassage" : "Clearing", french ? "assiette à ranger" : "plate to put away"));
                return card;
            }

            card.Title = table == null ? "" : (french ? "Table libre" : "Free table");
            card.TitleColor = Dim;
            card.Dish = table == null ? "" : (french ? "En attente d'un client" : "Waiting for a customer");
            card.DishColor = Dim;
            card.DishStyle = FontStyle.Italic;
            card.Border = EdgeQuiet;
            card.Fill = PanelQuiet;
            return card;
        }

        /// <summary>« Fais une salade de fruits » : l'article suit le nom de tête du plat.</summary>
        private static string PrepareHint(string dish, bool french)
        {
            if (string.IsNullOrEmpty(dish)) return french ? "Fais ce plat" : "Make this dish";
            string lower = char.ToLowerInvariant(dish[0]) + dish.Substring(1);
            if (!french) return $"Make a {lower}";
            bool feminine = lower.StartsWith("salade") || lower.StartsWith("soupe");
            return $"Fais {(feminine ? "une" : "un")} {lower}";
        }

        private void ShowCard(CardView view, CardModel model)
        {
            float edge = model.Thick ? 3f : 2f;
            Frame(view.Border, view.X, view.Y, CardWidth, CardHeight, model.Border);
            Frame(view.Fill, view.X + edge, view.Y + edge, CardWidth - 2f * edge, CardHeight - 2f * edge, model.Fill);

            Write(view.Title, model.Title);
            Tint(view.Title, model.TitleColor);

            bool bar = model.Bar >= 0f;
            float barX = view.X + CardWidth - 18f - 110f;
            view.Track.gameObject.SetActive(bar);
            view.Bar.gameObject.SetActive(bar && model.Bar > 0f);
            if (bar) Frame(view.Bar, barX, view.Y + 25f, 110f * Mathf.Clamp01(model.Bar), 12f, model.BarColor);

            Place(view.Right, bar ? barX - 8f : view.X + CardWidth - 18f, view.Y + 18f);
            view.Right.fontStyle = model.RightBold ? FontStyle.Bold : FontStyle.Normal;
            Write(view.Right, model.Right);
            Tint(view.Right, model.RightColor);

            view.Dish.fontStyle = model.DishStyle;
            Write(view.Dish, model.Dish);
            Tint(view.Dish, model.DishColor);

            for (int s = 0; s < view.Segments.Length; s++)
            {
                bool on = model.Segments != null;
                view.Segments[s].gameObject.SetActive(on);
                if (on) Paint(view.Segments[s], model.Segments[s]);
            }

            Write(view.Slot, model.Slot);
        }

        private static Piece Step(string step, string text, Color? color = null)
            => new(step, " — " + text, color ?? Cream);

        // Pas de gras : le jaune suffit à la distinguer, et en gras « Fais une salade de
        // fruits » prendrait deux lignes, faisant tomber la condition (« en regardant le cuisinier »).
        private static Piece Hint(string sentence, bool french) => new(null, Quote(sentence, french), Yellow);

        private static Piece Note(string text) => new(null, text, Dim);

        private static Piece Plain(string text, Color color) => new(null, text, color);

        /// <summary>
        /// La case d'état : ses morceaux découpés en DEUX lignes au plus — ce qui dépasse est
        /// omis, la carte garde sa hauteur et la tablette ne saute pas.
        /// </summary>
        private string Lines(params Piece[] pieces)
        {
            const float size = 22f;
            var lines = new List<string>();
            foreach (Piece piece in pieces)
            {
                if (string.IsNullOrEmpty(piece.Text) && piece.Bold == null) continue;
                string plain = (piece.Bold ?? "") + piece.Text;
                List<string> wrapped = Wrap(plain, SlotWidth, size);
                for (int i = 0; i < wrapped.Count && lines.Count < 2; i++)
                {
                    string line = wrapped[i];
                    if (i == 0 && piece.Bold != null && line.StartsWith(piece.Bold))
                        line = $"<b>{piece.Bold}</b>{line.Substring(piece.Bold.Length)}";
                    lines.Add(Colored(line, piece.Color));
                }
            }
            return string.Join("\n", lines);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Les départs, le pied
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Les « vies » de la partie : une pastille rouge par client parti sans être servi.</summary>
        private void ShowDepartures(int gone, int max, bool french)
        {
            string pips;
            if (max > 0)
            {
                var builder = new StringBuilder();
                for (int i = 0; i < max; i++)
                {
                    if (i > 0) builder.Append(' ');
                    builder.Append(i < gone ? Colored("●", Red) : Colored("○", Cream));
                }
                pips = builder.ToString();
            }
            else pips = gone.ToString();

            Write(_departPips, pips);
            float width = Measure(Regex.Replace(pips, "<[^>]+>", ""), 26f);
            Place(_departLabel, Right - width - 8f, TranscriptTop + 2f);
            Write(_departLabel, french ? "Partis" : "Left");
        }

        private void ShowFooter(CustomerOrder[] everyone, Sc4ve.Multimodality.Cook cook, List<string> queue,
                                Delegation waiter, bool french)
        {
            string cookText = cook == null ? "—"
                : cook.IsBusy
                    ? (french ? "prépare " : "making ") + RecipeLabel(cook.TaskLabel) + Waiting(queue.Count, french)
                    : (french ? "libre" : "free");
            Write(_cook, $"{Colored(french ? "Cuisinier :" : "Cook:", Dim)} {cookText}");
            Write(_waiter, $"{Colored(french ? "Serveur :" : "Waiter:", Dim)} {WaiterText(waiter, french)}");

            int served = everyone.Count(c => c.State == CustomerOrder.Stage.Served);
            int refusals = everyone.Sum(c => c.Refusals);
            Write(_served, $"{Colored(french ? "Servis" : "Served", Dim)} {served}");
            Write(_refusals, $"{Colored(french ? "Refus" : "Refusals", Dim)} {refusals}");
        }

        private static string WaiterText(Delegation waiter, bool french)
        {
            if (waiter == null) return "—";
            string who = waiter.Target != null ? CustomerOrder.At(waiter.Target)?.name : null;
            string text = waiter.CurrentErrand switch
            {
                Delegation.Errand.TakeOrder => french
                    ? "prend la commande " + (who != null ? Of(who) : "d'une table")
                    : "taking the order of " + (who ?? "a table"),
                Delegation.Errand.Serve => french ? "sert " + (who ?? "une table") : "serving " + (who ?? "a table"),
                Delegation.Errand.Clear => french ? "débarrasse une table" : "clearing a table",
                Delegation.Errand.Move => french ? "se déplace" : "moving",
                _ => french ? "libre" : "free",
            };
            return text + Waiting(waiter.QueuedOrders, french);
        }

        /// <summary>« de Marie », « d'Alice ».</summary>
        private static string Of(string name)
            => name.Length > 0 && "AEIOUYHÉÈÊÂÎÔ".IndexOf(char.ToUpperInvariant(name[0])) >= 0 ? $"d'{name}" : $"de {name}";

        private static string Waiting(int count, bool french)
            => count <= 0 ? "" : french ? $" · {count} en attente" : $" · {count} waiting";

        /// <summary>
        /// Le libellé d'une recette : celui qu'un client a déjà lu, sinon demandé une fois à
        /// l'ontologie (le nom local en attendant).
        /// </summary>
        private string RecipeLabel(string recipe)
        {
            if (string.IsNullOrEmpty(recipe)) return "";
            if (_recipeLabels.TryGetValue(recipe, out string label)) return label;
            if (_labelsAsked.Add(recipe)) _ = FetchLabel(recipe, UserData.Locale);
            int cut = recipe.LastIndexOf(':');
            return cut >= 0 ? recipe.Substring(cut + 1) : recipe;
        }

        private async Task FetchLabel(string recipe, string locale)
        {
            try
            {
                string label = await OntologyLabels.GetAsync(recipe, locale);
                if (locale == _labelsLocale) _recipeLabels[recipe] = label;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Tablette] Libellé de {recipe} illisible : {e.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Construction
        // ─────────────────────────────────────────────────────────────────────

        private void Build()
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Corps";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0f, Thickness / 2f);
            body.transform.localScale = new Vector3(WidthPx * Px, HeightPx * Px, Thickness);
            Destroy(body.GetComponent<Collider>());
            Settle(body.GetComponent<Renderer>(), Ground);

            // La voix.
            _micDot = NewText("Micro", 24f, TextAnchor.UpperLeft);
            Place(_micDot, Left, TranscriptTop);
            Write(_micDot, "●");
            _transcript = NewText("Phrase", 24f, TextAnchor.UpperLeft);
            Place(_transcript, Left + 24f, TranscriptTop);
            _departPips = NewText("Départs", 26f, TextAnchor.UpperRight);
            Place(_departPips, Right, TranscriptTop - 1f);
            _departLabel = NewText("Partis", 22f, TextAnchor.UpperRight);
            Tint(_departLabel, Dim);

            for (int i = 0; i < _chips.Length; i++)
            {
                _chips[i] = new Chip
                {
                    Border = NewQuad($"Étape {i + 1} — cadre", BorderZ),
                    Fill = NewQuad($"Étape {i + 1} — fond", FillZ),
                    Name = NewText($"Étape {i + 1} — nom", 20f, TextAnchor.UpperLeft, FontStyle.Bold),
                    Status = NewText($"Étape {i + 1} — état", 18f, TextAnchor.UpperLeft),
                };
                Show(_chips[i], false);
            }
            for (int i = 0; i < _arrows.Length; i++)
            {
                _arrows[i] = NewText($"Flèche {i + 1}", 20f, TextAnchor.UpperLeft);
                Write(_arrows[i], "→");
                Tint(_arrows[i], Dim);
                _arrows[i].gameObject.SetActive(false);
            }

            _outcome = NewText("Résultat", 22f, TextAnchor.UpperLeft);
            Place(_outcome, Left, OutcomeTop);
            Frame(NewQuad("Filet de la voix", BorderZ), Left, VoiceRule, Right - Left, 2f, EdgeQuiet);

            // La salle.
            float segment = (CardWidth - 36f - 18f) / 4f;
            for (int i = 0; i < _cards.Length; i++)
            {
                var card = new CardView
                {
                    X = Left + (i % 2) * (CardWidth + Gap),
                    Y = GridTop + (i / 2) * (CardHeight + Gap),
                };
                string prefix = $"Table {i + 1}";
                card.Border = NewQuad($"{prefix} — cadre", BorderZ);
                card.Fill = NewQuad($"{prefix} — fond", FillZ);
                card.Track = NewQuad($"{prefix} — jauge", TrackZ);
                Frame(card.Track, card.X + CardWidth - 18f - 110f, card.Y + 25f, 110f, 12f, Track);
                card.Bar = NewQuad($"{prefix} — niveau", MarkZ);
                for (int s = 0; s < card.Segments.Length; s++)
                {
                    card.Segments[s] = NewQuad($"{prefix} — étape {s + 1}", TrackZ);
                    Frame(card.Segments[s], card.X + 18f + s * (segment + 6f), card.Y + 90f, segment, 10f, Todo);
                }

                card.Title = NewText($"{prefix} — nom", 30f, TextAnchor.UpperLeft, FontStyle.Bold);
                Place(card.Title, card.X + 18f, card.Y + 14f);
                card.Right = NewText($"{prefix} — secondes", 22f, TextAnchor.UpperRight);
                card.Dish = NewText($"{prefix} — plat", 26f, TextAnchor.UpperLeft);
                Place(card.Dish, card.X + 18f, card.Y + 54f);
                card.Slot = NewText($"{prefix} — état", 22f, TextAnchor.UpperLeft);
                Place(card.Slot, card.X + 18f, card.Y + 106f);
                _cards[i] = card;
            }

            // Le pied.
            Frame(NewQuad("Filet du pied", BorderZ), Left, FooterRule, Right - Left, 2f, EdgeQuiet);
            _cook = NewText("Cuisinier", 24f, TextAnchor.UpperLeft);
            Place(_cook, Left, 602f);
            _waiter = NewText("Serveur", 24f, TextAnchor.UpperLeft);
            Place(_waiter, Left, 628f);
            _served = NewText("Servis", 24f, TextAnchor.UpperRight);
            Place(_served, Right, 602f);
            _refusals = NewText("Refus", 24f, TextAnchor.UpperRight);
            Place(_refusals, Right, 628f);
        }

        private Renderer NewQuad(string name, float z)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, z);
            Destroy(quad.GetComponent<Collider>());
            Renderer renderer = quad.GetComponent<Renderer>();
            Settle(renderer, Ground);
            return renderer;
        }

        /// <summary>Un écran, pas un objet de la salle : ni ombre portée, ni ombre reçue.</summary>
        private static void Settle(Renderer renderer, Color color)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.material.color = color;
        }

        private TextMesh NewText(string name, float sizePx, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = new Vector3(0f, 0f, TextZ);

            var mesh = holder.AddComponent<TextMesh>();
            mesh.font = _font;
            mesh.fontSize = FontSize;
            mesh.fontStyle = style;
            mesh.characterSize = Character(sizePx);
            mesh.anchor = anchor;
            mesh.alignment = anchor == TextAnchor.UpperRight ? TextAlignment.Right : TextAlignment.Left;
            mesh.richText = true;
            mesh.color = Cream;

            MeshRenderer renderer = holder.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (_font != null) renderer.sharedMaterial = _font.material;
            return mesh;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Outils
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// La taille de caractère d'un TextMesh dont le cadratin mesure <paramref name="sizePx"/>
        /// pixels de la maquette : un TextMesh mesure fontSize × characterSize / 10 unités par
        /// cadratin.
        /// </summary>
        private static float Character(float sizePx) => sizePx * Px * 10f / FontSize;

        /// <summary>Pose un quad sur le rectangle (x, y, w, h) de la maquette, et le peint.</summary>
        private static void Frame(Renderer quad, float x, float y, float w, float h, Color color)
        {
            Transform t = quad.transform;
            t.localPosition = new Vector3((x + w / 2f - WidthPx / 2f) * Px, (HeightPx / 2f - y - h / 2f) * Px,
                                          t.localPosition.z);
            t.localScale = new Vector3(Mathf.Max(w, 0.01f) * Px, Mathf.Max(h, 0.01f) * Px, 1f);
            Paint(quad, color);
        }

        private static void Paint(Renderer quad, Color color)
        {
            if (quad.material.color != color) quad.material.color = color;
        }

        private static void Place(TextMesh text, float x, float y)
            => text.transform.localPosition = new Vector3((x - WidthPx / 2f) * Px, (HeightPx / 2f - y) * Px, TextZ);

        private static void Write(TextMesh text, string value)
        {
            value ??= "";
            if (text.text != value) text.text = value;
        }

        private static void Tint(TextMesh text, Color color)
        {
            if (text.color != color) text.color = color;
        }

        private static string Colored(string text, Color color)
            => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

        /// <summary>Guillemets français insécables : « » ne se séparent jamais de leur texte.</summary>
        private static string Quote(string text, bool french)
            => french ? $"« {text} »" : $"“{text}”";

        /// <summary>
        /// Largeur d'un texte, en pixels de la maquette, mesurée sur la police elle-même. Un
        /// glyphe absent (pris dans une police de repli du système) est estimé large.
        /// </summary>
        private float Measure(string text, float sizePx, FontStyle style = FontStyle.Normal)
        {
            if (string.IsNullOrEmpty(text) || _font == null) return 0f;
            _font.RequestCharactersInTexture(text, FontSize, style);
            float advance = 0f;
            foreach (char c in text)
                advance += _font.GetCharacterInfo(c, out CharacterInfo info, FontSize, style)
                    ? info.advance
                    : FontSize * 0.6f;
            return advance * sizePx / FontSize;
        }

        /// <summary>Découpe aux espaces — l'espace insécable ne coupe jamais.</summary>
        private List<string> Wrap(string text, float maxPx, float sizePx, FontStyle style = FontStyle.Normal)
        {
            var lines = new List<string>();
            string line = "";
            foreach (string word in (text ?? "").Split(' '))
            {
                if (word.Length == 0) continue;
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && Measure(candidate, sizePx, style) > maxPx)
                {
                    lines.Add(line);
                    line = word;
                }
                else line = candidate;
            }
            if (line.Length > 0) lines.Add(line);
            return lines;
        }

        private static Color Hex(string rgb)
            => ColorUtility.TryParseHtmlString("#" + rgb, out Color color) ? color : Color.magenta;
    }
}
