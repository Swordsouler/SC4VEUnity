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

        /// <summary>
        /// Énonce un texte via la synthèse vocale (Piper) si un PiperTextToSpeech est présent
        /// dans la scène. Sans effet (avertissement) sinon. La langue est gérée par le composant.
        /// </summary>
        public static void Speak(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            PiperTextToSpeech tts = UnityEngine.Object.FindFirstObjectByType<PiperTextToSpeech>();
            if (tts != null) tts.Speak(text);
            else Debug.LogWarning("[TTS] Aucun PiperTextToSpeech dans la scène — texte non énoncé.");
        }
    }
}