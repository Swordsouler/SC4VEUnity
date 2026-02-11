using Sven.Content;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality.Intent
{
    [Serializable]
    public class MoveCommand : Command
    {
        private SelectionParameter SelectionParameter => GetParameter<SelectionParameter>();
        private PointParameter PointParameter => GetParameter<PointParameter>();

        public override List<SemantizationCore> Execute()
        {
            List<SemantizationCore> objects = SelectionParameter.Objects;
            foreach (SemantizationCore semantizationCore in objects)
            {
                if (PointParameter == null || PointParameter.Point == null) continue;
                semantizationCore.transform.position = (Vector3)PointParameter.Point;
                Debug.Log($"Moving object {semantizationCore.GetUUID()} to position {PointParameter.Point}");
            }
            return objects;
        }
    }
}