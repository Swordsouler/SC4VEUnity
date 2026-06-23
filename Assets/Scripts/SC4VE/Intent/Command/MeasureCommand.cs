using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [RuleBasedTriggers("mesure", "mesurer", "calcule la distance", "calculer la distance", "quelle est la distance")]
    [Serializable, CommandDescription("Mesure une distance. Paramètres: multiples SelectionParameter et/ou PointParameter.")]
    public class MeasureCommand : Command
    {
        private PointParameter PointParameter1 => GetParameter<PointParameter>(1);
        private PointParameter PointParameter2 => GetParameter<PointParameter>(2);

        public override List<SemantizationCore> Execute()
        {
            if (PointParameter1 == null || PointParameter2 == null || PointParameter1.Point == null || PointParameter2.Point == null)
            {
                Debug.LogError("MeasureCommand requires two valid PointParameters.");
                return new();
            }
            float distance = Vector3.Distance((Vector3)PointParameter1.Point, (Vector3)PointParameter2.Point);
            Debug.Log($"Distance: {distance} units");
            Speak($"La distance est de {distance:0.0} mètres.");
            return new();
        }
    }
}