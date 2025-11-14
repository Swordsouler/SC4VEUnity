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
                semantizationCore.transform.position = PointParameter.Point;
                Debug.Log($"Moving object {semantizationCore.GetUUID()} to position {PointParameter.Point}");
            }
        }
    }
}