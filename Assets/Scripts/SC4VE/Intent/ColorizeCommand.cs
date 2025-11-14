using System;
using UnityEngine;

namespace Sc4ve.Multimodality.Parameter
{
    [Serializable]
    public class ColorizeCommand : Command
    {
        public SelectionParameter SelectionParameter
        {
            get
            {
                if (Parameters != null)
                {
                    foreach (Parameter parameter in Parameters)
                    {
                        if (parameter is SelectionParameter selectionParameter)
                        {
                            return selectionParameter;
                        }
                    }
                }
                return null;
            }
        }
        public ColorParameter ColorParameter
        {
            get
            {
                if (Parameters != null)
                {
                    foreach (Parameter parameter in Parameters)
                    {
                        if (parameter is ColorParameter colorParameter)
                        {
                            return colorParameter;
                        }
                    }
                }
                return null;
            }
        }

        public override void Execute()
        {
            Debug.Log("Executing ColorizeCommand");
        }
    }
}