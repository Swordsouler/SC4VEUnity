using System;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable]
    public class MeasureCommand : Command
    {
        private PointParameter PointParameter1 => GetParameter<PointParameter>(1);
        private PointParameter PointParameter2 => GetParameter<PointParameter>(2);

        public override void Execute()
        {
            if (PointParameter1 == null || PointParameter2 == null || PointParameter1.Point == null || PointParameter2.Point == null)
            {
                Debug.LogError("MeasureCommand requires two valid PointParameters.");
                return;
            }
            float distance = Vector3.Distance((Vector3)PointParameter1.Point, (Vector3)PointParameter2.Point);
            Debug.Log($"Distance: {distance} units");
        }
    }
}