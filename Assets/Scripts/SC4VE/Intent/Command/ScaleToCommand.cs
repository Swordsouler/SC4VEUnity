using Newtonsoft.Json;
using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable, CommandDescription("Règle la taille à une valeur absolue (localScale = N sur chaque axe). Ex: « mets la taille de cette citrouille à 2 ». Paramètres: SelectionParameter.")]
    [RuleBasedTriggers("mets la taille", "règle la taille", "fixe la taille", "définis la taille",
                       "ajuste la taille", "set the size", "set size", "scale to", "size to")]
    public class ScaleToCommand : Command
    {
        // Taille cible absolue : localScale = (Value, Value, Value). 1 par défaut (taille neutre)
        // pour rester valide si une commande LLM n'inclut pas « value ».
        [SerializeField] private float _value = 1f;
        [JsonProperty("value")]
        public float Value { get => _value; set => _value = value; }

        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
        {
            if (ctx.ScaleValue > 0f) Value = ctx.ScaleValue;
            return new List<Parameter> { ctx.BuildSelectionParameter(fallbackToSelection: FallbackToSelectionWhenEmpty) };
        }

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, semantizationCore =>
            {
                Transform t = semantizationCore.transform;
                Vector3 prev = t.localScale;
                Vector3 next = Vector3.one * Value;
                t.localScale = next;
                Debug.Log($"[ScaleTo] Objet {semantizationCore.GetUUID()} → localScale {next}");
                return (() => t.localScale = prev,
                        () => t.localScale = next);
            });
        }
    }
}
