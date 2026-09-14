using Newtonsoft.Json;
using Sc4ve.Voice;
using Sven.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VDS.RDF;

namespace Sc4ve.Multimodality.Intent
{
    [JsonConverter(typeof(CommandConverter))]
    [Serializable]
    public abstract class Command
    {
        [SerializeField] private string _id = string.Empty;
        public string Id
        {
            get
            {
                if (string.IsNullOrEmpty(_id))
                    _id = Guid.NewGuid().ToString();
                return _id;
            }
        }

        [SerializeField] private string _type;
        [JsonProperty("type")]
        public string Type
        {
            get => _type;
            set => _type = value;
        }

        [SerializeField] private List<Parameter> _parameters;
        [JsonProperty("parameters")]
        public List<Parameter> Parameters
        {
            get => _parameters;
            set => _parameters = value;
        }

        private static List<SemantizationCore> _lastObjects = new();
        public static List<string> LastObjectIds => _lastObjects?.Select(obj => obj.GetUUID()).ToList() ?? new List<string>();
        public static List<SemantizationCore> LastObjects
        {
            get => _lastObjects;
            set
            {
                if (_lastObjects == value) return;
                // be sure it's unique objects
                _lastObjects = value?.GroupBy(obj => obj.GetUUID()).Select(group => group.First()).ToList();
            }
        }

        public async Task<IUriNode> Semanticize(Graph graph)
        {
            // Create a URI node for the command
            IUriNode commandNode = graph.CreateUriNode($":{Id}");
            IUriNode rdfType = graph.CreateUriNode("rdf:type");
            IUriNode commandType = graph.CreateUriNode($"sc4ve:{Type}");
            // Add the type triple
            graph.Assert(new Triple(commandNode, rdfType, commandType));
            // Add parameters
            if (Parameters != null)
            {
                IUriNode hasParameter = graph.CreateUriNode($"sc4ve:hasParameter");
                foreach (Parameter parameter in Parameters)
                {
                    IUriNode parameterNode = await parameter.Semanticize(graph);
                    graph.Assert(new Triple(commandNode, hasParameter, parameterNode));
                }
            }
            return commandNode;
        }

        /// <summary>
        /// Repli sur la sélection courante quand la cible (déictique/pointage, ou absence de cible)
        /// résout à vide. Vrai par défaut (commandes de transformation : « triple ça » / « cache
        /// ça » agissent sur la sélection si rien n'est pointé). Les commandes qui DÉFINISSENT la
        /// sélection (Select/Unselect) le passent à false.
        /// </summary>
        protected virtual bool FallbackToSelectionWhenEmpty => true;

        /// <summary>
        /// Construit les paramètres de la commande depuis le contexte RuleBased.
        /// Par défaut : un seul SelectionParameter standard (repli sélection selon
        /// <see cref="FallbackToSelectionWhenEmpty"/>).
        /// Surcharger pour les commandes avec une logique spécifique (MoveCommand, ColorizeCommand…).
        /// </summary>
        public virtual List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
            => new List<Parameter> { ctx.BuildSelectionParameter(fallbackToSelection: FallbackToSelectionWhenEmpty) };

        public abstract List<SemantizationCore> Execute();

        protected T GetParameter<T>(int element = 1) where T : Parameter
        {
            return Parameters?.OfType<T>().Skip(element - 1).FirstOrDefault();
        }

        /// <summary>Premier SelectionParameter de la commande (cible par défaut), ou null.</summary>
        protected SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();

        /// <summary>
        /// Applique une mutation réversible à chaque objet et pousse UNE entrée undo/redo
        /// dans <see cref="CommandHistory"/>. <paramref name="mutate"/> applique la mutation
        /// à l'objet et retourne la paire (undo, redo) correspondante, ou null pour ignorer
        /// l'objet. Les closures sont protégées contre les objets détruits entre-temps
        /// (undo/redo devient un no-op pour eux, au lieu d'une MissingReferenceException).
        /// </summary>
        protected static List<SemantizationCore> ExecuteReversible(
            List<SemantizationCore> objects,
            Func<SemantizationCore, (Action undo, Action redo)?> mutate)
        {
            var undoActions = new List<Action>();
            var redoActions = new List<Action>();
            foreach (SemantizationCore obj in objects ?? new List<SemantizationCore>())
            {
                if (obj == null) continue;
                (Action undo, Action redo)? actions = mutate(obj);
                if (actions == null) continue;
                SemantizationCore captured = obj;
                Action undo = actions.Value.undo;
                Action redo = actions.Value.redo;
                undoActions.Add(() => { if (captured != null) undo(); });
                redoActions.Add(() => { if (captured != null) redo(); });
            }
            if (undoActions.Count > 0)
                CommandHistory.Push(
                    () => undoActions.ForEach(a => a()),
                    () => redoActions.ForEach(a => a()));
            return objects ?? new List<SemantizationCore>();
        }

        /// <summary>
        /// Énonce un texte via la synthèse vocale (Piper) si un PiperTextToSpeech est présent
        /// dans la scène. Sans effet (avertissement) sinon. La langue est gérée par le composant.
        /// </summary>
        public static void Speak(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            PiperTextToSpeech tts = UnityEngine.Object.FindAnyObjectByType<PiperTextToSpeech>();
            if (tts != null) tts.Speak(text);
            else Debug.LogWarning("[TTS] Aucun PiperTextToSpeech dans la scène — texte non énoncé.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Question posée depuis Execute (rôle manquant)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// La commande qui attend une réponse, ou null. Relevée par le contrôleur après
        /// l'exécution, qui la met en attente pour l'énoncé suivant.
        ///
        /// À distinguer du manque de PARAMÈTRE (ClarificationVocabulary, restrictions OWL) : ici
        /// la commande est structurellement complète — TakeOrderCommand a bien son unique
        /// SelectionParameter — mais les RÔLES lus dans les objets désignés ne le sont pas. Une
        /// cardinalité OWL ne peut pas voir cette différence ; seul Execute le peut, puisque les
        /// rôles se lisent dans les objets et non dans la phrase (cf. DelegationRoles).
        ///
        /// Sans ce relais, « Quel serveur ? » était une question sans oreille : rien n'était mis
        /// en attente, et la réponse « ce serveur-là 👆 » retombait sur « je n'ai pas compris ».
        /// </summary>
        public static Command AwaitingAnswer { get; private set; }

        /// <summary>Oublie la commande en attente. Appelé avant chaque exécution.</summary>
        public static void ForgetAwaitingAnswer() => AwaitingAnswer = null;

        /// <summary>
        /// Vrai si la question de cette commande appelle une CIBLE en réponse (« Quel serveur ? »
        /// → « ce serveur-là 👆 ») plutôt qu'un paramètre (une couleur, une destination).
        /// </summary>
        [JsonIgnore] public virtual bool ExpectsTargetAnswer => false;

        /// <summary>
        /// Pose une question et se met en attente : l'énoncé suivant complétera CETTE commande au
        /// lieu d'être interprété seul.
        ///
        /// L'appelant doit renvoyer les cibles déjà trouvées depuis son Execute : le contrôleur
        /// en fait la sélection courante, ce qui les montre au joueur ET donne à la réponse de
        /// quoi s'unir (SelectionParameter.UnionWithSelection). Sans cela, la réponse
        /// REMPLACERAIT le rôle déjà acquis et le dialogue tournerait en rond.
        /// </summary>
        protected void Ask(string question)
        {
            Speak(question);
            AwaitingAnswer = this;
        }
    }
}