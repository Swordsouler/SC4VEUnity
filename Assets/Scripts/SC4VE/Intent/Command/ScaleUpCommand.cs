using Newtonsoft.Json;
using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription("Change la taille (agrandissement). Le facteur multiplicatif se règle via la propriété « factor » de la commande (1.1 par défaut ; « double » → 2, « triple » → 3 ; « un peu » → 1.05, « beaucoup » → 1.2). Paramètres: SelectionParameter.")]
    [RuleBasedTriggers("augmente la taille", "scale up", "grossis", "grossit", "agrandis",
                       "agrandit", "grandit", "grandir", "grossir", "agrandir",
                       "double", "doubler", "triple", "tripler")]
    public class ScaleUpCommand : Command
    {
        // Facteur d'agrandissement. ×1.1 par défaut (« agrandis ») ; redéfini par « double » (×2)
        // ou « triple » (×3). Initialisé à 1.1 pour que les commandes du LLM (JSON sans « factor »)
        // restent valides.
        [SerializeField] private float _factor = 1.1f;
        [JsonProperty("factor")]
        public float Factor { get => _factor; set => _factor = value; }

        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
        {
            if (ctx.ScaleFactor > 0f) Factor = ctx.ScaleFactor;
            // Adverbe graduable : il module l'ÉCART du facteur à la taille neutre (×1), pas le
            // facteur brut — « agrandis-le un peu » doit rester un agrandissement (×1.05 ; un
            // ×0.55 serait une réduction). « beaucoup » → ×1.2, plus marqué que le ×1.1 par défaut.
            else if (ctx.MagnitudeModifier != 1f) Factor = 1f + (Factor - 1f) * ctx.MagnitudeModifier;
            return new List<Parameter> { ctx.BuildSelectionParameter(fallbackToSelection: FallbackToSelectionWhenEmpty) };
        }

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, semantizationCore =>
            {
                Transform t = semantizationCore.transform;
                Vector3 prev = t.localScale;
                Vector3 next = prev * Factor;
                t.localScale = next;
                Debug.Log($"Scaling up object {semantizationCore.GetUUID()} to {t.localScale}");
                return (() => t.localScale = prev,
                        () => t.localScale = next);
            });
        }
    }
}