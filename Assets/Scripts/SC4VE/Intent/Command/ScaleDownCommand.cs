using Newtonsoft.Json;
using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription("Change la taille (réduction). Le facteur de division se règle via la propriété « factor » de la commande (1.1 par défaut ; « un peu » → 1.05, « beaucoup » → 1.2). Paramètres: SelectionParameter.")]
    [RuleBasedTriggers("diminue la taille", "scale down", "rapetisse", "rapetissit", "réduis",
                       "réduit", "diminue", "rétrécis", "rétrécit", "rapetisser", "réduire", "rétrécir")]
    public class ScaleDownCommand : Command
    {
        // Facteur de RÉDUCTION (diviseur) : localScale = précédent / Factor. ×1.1 par défaut,
        // symétrique de ScaleUpCommand. Initialisé à 1.1 pour que les commandes du LLM
        // (JSON sans « factor ») restent valides.
        [SerializeField] private float _factor = 1.1f;
        [JsonProperty("factor")]
        public float Factor { get => _factor; set => _factor = value; }

        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
        {
            // Même modulation graduable que ScaleUpCommand : l'adverbe module l'ÉCART du
            // facteur à la taille neutre (×1) — « réduis-le un peu » divise par 1.05,
            // « réduis-le beaucoup » par 1.2.
            if (ctx.MagnitudeModifier != 1f) Factor = 1f + (Factor - 1f) * ctx.MagnitudeModifier;
            return new List<Parameter> { ctx.BuildSelectionParameter(fallbackToSelection: FallbackToSelectionWhenEmpty) };
        }

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, semantizationCore =>
            {
                Transform t = semantizationCore.transform;
                Vector3 prev = t.localScale;
                Vector3 next = prev / Factor;
                t.localScale = next;
                Debug.Log($"Scaling down object {semantizationCore.GetUUID()} to {t.localScale}");
                return (() => t.localScale = prev,
                        () => t.localScale = next);
            });
        }
    }
}
