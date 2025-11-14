using Sven.Content;
using System;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable]
    public class MoveCommand : Command
    {
        public SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();
        public PointParameter PointParameter => GetParameter<PointParameter>();

        public override void Execute()
        {
            foreach (SemantizationCore semantizationCore in SelectionParameter.Objects)
            {
                Debug.Log($"Preparing to move object {semantizationCore.GetUUID()}");
                if (PointParameter == null || PointParameter.Point == null) continue;
                semantizationCore.transform.position = (Vector3)PointParameter.Point;
                Debug.Log($"Moving object {semantizationCore.GetUUID()} to position {PointParameter.Point}");
            }
        }
    }
}