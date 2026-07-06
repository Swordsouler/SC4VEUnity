using Sven.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("mesure", "mesurer", "calcule la distance", "calculer la distance", "quelle est la distance")]
    [Serializable, CommandDescription("Mesure une distance. Paramètres: multiples SelectionParameter et/ou PointParameter.")]
    public class MeasureCommand : Command
    {
        // « mesure la distance entre ça et ça » : un PointParameter par déictique (max 2),
        // résolus par timestamp — comme la destination de MoveCommand. Le builder par défaut
        // (un seul SelectionParameter) ne fournissait jamais les deux points requis.
        public override List<Parameter> BuildRuleBasedParameters(RuleBasedContext ctx)
        {
            var ps = new List<Parameter>();
            foreach (RuleBasedAnnotation deictic in (ctx.Deictics ?? new List<RuleBasedAnnotation>()).Take(2))
                ps.Add(new PointParameter { Type = "PointParameter", Value = ctx.PointerName, Timestamp = deictic.Timestamp });
            // Repli : « la distance entre les deux pommes » → objets filtrés/sélectionnés.
            if (ps.Count < 2)
                ps.Add(ctx.BuildSelectionParameter(fallbackToSelection: true));
            return ps;
        }

        public override List<SemantizationCore> Execute()
        {
            // Deux points, pris dans l'ordre des paramètres : PointParameter (pointage) et/ou
            // positions d'objets d'un SelectionParameter (mode LLM : deux sélections).
            List<Vector3> points = new();
            foreach (Parameter parameter in Parameters ?? new List<Parameter>())
            {
                if (points.Count >= 2) break;
                if (parameter is PointParameter pp && pp.Point != null)
                    points.Add((Vector3)pp.Point);
                else if (parameter is SelectionParameter sp)
                    foreach (SemantizationCore obj in sp.Objects ?? new List<SemantizationCore>())
                    {
                        points.Add(obj.transform.position);
                        if (points.Count >= 2) break;
                    }
            }

            if (points.Count < 2)
            {
                Debug.LogWarning("MeasureCommand : deux points requis (pointages ou objets) — commande ignorée.");
                return new();
            }
            float distance = Vector3.Distance(points[0], points[1]);
            Debug.Log($"Distance: {distance} units");
            Speak($"La distance est de {distance:0.0} mètres.");
            return new();
        }
    }
}