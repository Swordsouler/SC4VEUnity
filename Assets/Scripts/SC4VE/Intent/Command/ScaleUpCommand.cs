using Newtonsoft.Json;
using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription("Change la taille (agrandissement). Paramètres: SelectionParameter.")]
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