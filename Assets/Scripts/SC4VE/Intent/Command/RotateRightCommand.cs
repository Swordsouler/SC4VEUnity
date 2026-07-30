using Newtonsoft.Json;
using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("tourne à droite", "pivote à droite", "rotation droite", "tourne droite", "tourne", "pivote", "faire pivoter", "rotation", "quart de tour", "demi-tour")]
    [Serializable, CommandDescription("Fait pivoter les objets vers la droite (axe Y). L'angle en degrés se règle via la propriété « angle » de la commande (45 par défaut ; « de 90 degrés » → \"angle\": \"90\" ; « un peu » → 22.5, « beaucoup » → 90). Paramètres: SelectionParameter.")]
    public class RotateRightCommand : Command
    {
        // Angle de rotation en degrés. 45 par défaut pour que les commandes LLM sans « angle »
        // gardent le comportement historique.
        [SerializeField] private float _angle = 45f;
        [JsonProperty("angle")]
        public float Angle { get => _angle; set => _angle = value; }

        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
        {
            // Grandeur discrète explicite (« de 90° ») prioritaire sur l'adverbe graduable ;
            // sinon celui-ci module l'incrément par défaut (« un peu » → 22,5°, « beaucoup » → 90°).
            if (ctx.Angle > 0f) Angle = ctx.Angle;
            else Angle *= ctx.MagnitudeModifier;
            return new List<Parameter> { ctx.BuildSelectionParameter(fallbackToSelection: FallbackToSelectionWhenEmpty) };
        }

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter?.Objects ?? new();
            return ExecuteReversible(objects, obj =>
            {
                var prev = obj.transform.rotation;
                obj.transform.Rotate(Vector3.up, Angle, Space.World);
                var next = obj.transform.rotation;
                return (() => obj.transform.rotation = prev,
                        () => obj.transform.rotation = next);
            });
        }
    }
}
